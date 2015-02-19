using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;
using System.Threading;
using System.Data;
using System.Collections.Concurrent;
using System.Collections;
using System.Diagnostics;
using Syrilium.CommonInterface;
using System.Linq.Expressions;
using Syrilium.Common;
using Syrilium.CachingInterface;

namespace Syrilium.Caching
{
	public class Cache : ICache
	{
		private static AssemblyBuilder asmBuilder = null;
		private static ModuleBuilder modBuilder = null;
		private static Type typeOfObject;
		private static Type typeOfObjectArray;
		private static Type typeOfVoid;
		private static Type typeOfCache;
		private static Cryptography cryptography;
		private static ReflectionHelper reflectionHelper;
		private static MethodInfo getCachedMethod;
		private static ReaderWriterLockWrapper<Dictionary<string, DerivedTypeCache>> hashDerivedTypeMapping;
		/// <summary>
		/// Dictionary[Hash, Tuple[BaseMethodInfo, CallBaseMethodInfo, ParametersTypes, DerivedTypeCache]]
		/// </summary>
		private static ReaderWriterLockWrapper<Dictionary<string, DerivedMethodCache>> hashMethodMapping { get; set; }
		private static int lastClearTime;
		private static long startingSecond;
		private static System.Timers.Timer clearTimer;
		private static ReaderWriterLockWrapper<Dictionary<int, Dictionary<Cache, List<CacheInfo>>>> valuesExpiration;

		private ReaderWriterLockWrapper<Dictionary<string, CacheInfo>> values;
		private ReaderWriterLockWrapper<Dictionary<Type, DerivedTypeCache>> typeDerivedMapping;
		private ReaderWriterLockWrapper<List<object>> clearBufferResult;
		private ReaderWriterLockWrapper<List<KeyValuePair<Type, bool>>> clearBufferCachedType;
		private bool inConfigurationsCollectionChanged = false;

		public ReaderWriterLockWrapper<ObservableCollection<ICacheTypeConfiguration>> Configurations { get; private set; }
		public ICacheConfiguration Configure { get; private set; }

		static Cache()
		{
			asmBuilder = Thread.GetDomain().DefineDynamicAssembly(new AssemblyName("Syrilium.Cache"), AssemblyBuilderAccess.Run);
			modBuilder = asmBuilder.DefineDynamicModule(asmBuilder.GetName().Name, false);
			hashDerivedTypeMapping = new ReaderWriterLockWrapper<Dictionary<string, DerivedTypeCache>>();
			hashMethodMapping = new ReaderWriterLockWrapper<Dictionary<string, DerivedMethodCache>>();
			valuesExpiration = new ReaderWriterLockWrapper<Dictionary<int, Dictionary<Cache, List<CacheInfo>>>>();

			startingSecond = (long)Math.Floor(TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds) - 1;
			clearTimer = new System.Timers.Timer(1000);
			clearTimer.AutoReset = false;
			clearTimer.Elapsed += clearTimer_Elapsed;
			clearTimer.Start();

			typeOfCache = typeof(Cache);
			typeOfObject = typeof(object);
			typeOfObjectArray = typeof(object[]);
			typeOfVoid = typeof(void);

			getCachedMethod = typeOfCache.GetMethod("GetCached", BindingFlags.Public | BindingFlags.Static);

			cryptography = new Cryptography();
			reflectionHelper = new ReflectionHelper();
		}

		public Cache()
		{
			values = new ReaderWriterLockWrapper<Dictionary<string, CacheInfo>>();
			clearBufferResult = new ReaderWriterLockWrapper<List<object>>();
			clearBufferCachedType = new ReaderWriterLockWrapper<List<KeyValuePair<Type, bool>>>();
			typeDerivedMapping = new ReaderWriterLockWrapper<Dictionary<Type, DerivedTypeCache>>();

			var typeColl = new ObservableCollection<ICacheTypeConfiguration>();
			typeColl.CollectionChanged += configurations_CollectionChanged;
			Configurations = new ReaderWriterLockWrapper<ObservableCollection<ICacheTypeConfiguration>>(typeColl);
			Configure = new CacheConfiguration();
			((CacheConfiguration)Configure).OnNewType += Cache_OnNewType;
		}

		private static void clearTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			try
			{
				valuesExpiration.Read(ver =>
					{
						var now = (int)((long)Math.Floor(TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds) - 1 - startingSecond);
						var secondsToClear = new List<int>();
						for (int i = lastClearTime + 1; i <= now; i++)
						{
							if (ver.Value.ContainsKey(i))
								secondsToClear.Add(i);
						}

						if (secondsToClear.Count > 0)
						{
							ver.Write(ve =>
								{
									if (ve.AnyWritersSince)
									{
										for (int i = 0; i < secondsToClear.Count; i++)
										{
											if (!ve.Value.ContainsKey(secondsToClear[i]))
												secondsToClear.RemoveAt(i--);
										}
									}
									foreach (var sec in secondsToClear)
									{
										foreach (var cv in ve.Value[sec])
										{
											cv.Key.values.Write(cvw =>
												cv.Value.ForEach(v =>
												{
													cvw.Remove(v.Key);
													v.Dispose();
												}));
										}
										ve.Value.Remove(sec);
									}
								});
						}

						lastClearTime = now;
					});
			}
			finally
			{
				clearTimer.Start();
			}
		}

		private void configurations_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				if (inConfigurationsCollectionChanged) return;
				try
				{
					inConfigurationsCollectionChanged = true;
					var types = (ObservableCollection<ICacheTypeConfiguration>)sender;
					var typesToRemove = new List<ICacheTypeConfiguration>();
					foreach (ICacheTypeConfiguration newItem in e.NewItems)
					{
						if (types.Count(t => t.Type == newItem.Type) > 1)
						{
							typesToRemove.Add(newItem);
							continue;
						}

						var nearestDerivedType = types.FirstOrDefault(c => newItem.Type.IsAssignableFrom(c.Type));
						if (nearestDerivedType != null && newItem != nearestDerivedType)
						{
							types.Remove(newItem);
							types.Insert(types.IndexOf(nearestDerivedType), newItem);
						}
					}
					if (typesToRemove.Count > 0)
					{
						foreach (var t in typesToRemove)
							types.Remove(t);
						throw new InvalidOperationException("Type already exists.");
					}
				}
				finally
				{
					inConfigurationsCollectionChanged = false;
				}
			}
		}

		private ICacheTypeConfiguration Cache_OnNewType(CacheTypeConfiguration ctc)
		{
			ICacheTypeConfiguration typeConfig = null;
			return Configurations.ConditionalReadWrite(
				cfg => (typeConfig = cfg.Find(c => c.Type == ctc.Type)) == null,
				cfg => typeConfig,
				cfg =>
				{
					ctc.Init();
					cfg.Add(ctc);
					return ctc;
				});
		}

		private static TypeBuilder createType(ModuleBuilder modBuilder, string typeName, Type baseType)
		{
			TypeBuilder typeBuilder = modBuilder.DefineType(typeName,
						TypeAttributes.Public |
						TypeAttributes.Class |
						TypeAttributes.AutoClass |
						TypeAttributes.AnsiClass |
						TypeAttributes.BeforeFieldInit |
						TypeAttributes.AutoLayout,
						baseType);

			createCacheProperty(typeBuilder);

			return typeBuilder;
		}

		private static void createConstructor(TypeBuilder typeBuilder, Type baseType)
		{
			ConstructorBuilder constructor = typeBuilder.DefineConstructor(
								MethodAttributes.Public |
								MethodAttributes.SpecialName |
								MethodAttributes.RTSpecialName,
								CallingConventions.Standard,
								new Type[0]);
			//Define the reflection ConstructorInfor for System.Object
			ConstructorInfo conObj = baseType.GetConstructor(new Type[0]);

			//call constructor of base object
			ILGenerator il = constructor.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, conObj);
			il.Emit(OpCodes.Ret);
		}

		private static string createMethodCallBase(TypeBuilder typeBuilder, MethodInfo methodOriginal, Type[] paramTypes)
		{
			string methodCallBaseName = string.Concat(methodOriginal.MethodHandle.Value.ToString(), "_", methodOriginal.Name);
			MethodBuilder methodOverride = typeBuilder.DefineMethod(methodCallBaseName,
			MethodAttributes.Private |
			MethodAttributes.SpecialName |
			MethodAttributes.RTSpecialName |
			MethodAttributes.ReuseSlot |
			MethodAttributes.HideBySig,
			CallingConventions.Standard,
			methodOriginal.ReturnType,
			paramTypes);

			ILGenerator il = methodOverride.GetILGenerator();

			il.Emit(OpCodes.Ldarg_0);
			for (int i = 0; i < paramTypes.Length; i++)
			{
				il.Emit(OpCodes.Ldarg, i + 1);
			}
			il.Emit(OpCodes.Call, methodOriginal);
			il.Emit(OpCodes.Ret);

			return methodCallBaseName;
		}

		private static void createCacheProperty(TypeBuilder typeBuilder)
		{
			FieldBuilder propertyFieldBuilder = typeBuilder.DefineField("__cache__", typeOfCache, FieldAttributes.Private);

			PropertyBuilder pBuilder = typeBuilder.DefineProperty("__Cache__", System.Reflection.PropertyAttributes.HasDefault, typeOfCache, null);

			MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

			MethodBuilder getPropertyBuilder = typeBuilder.DefineMethod("get___Cache__", getSetAttr, typeOfCache, Type.EmptyTypes);

			// Constructing IL Code for get and set Methods.
			ILGenerator getPropGenerator = getPropertyBuilder.GetILGenerator();
			getPropGenerator.Emit(OpCodes.Ldarg_0);
			getPropGenerator.Emit(OpCodes.Ldfld, propertyFieldBuilder);
			getPropGenerator.Emit(OpCodes.Ret);

			MethodBuilder setPropertyBuulder = typeBuilder.DefineMethod("set___Cache__", getSetAttr, null, new Type[] { typeOfCache });

			ILGenerator setPropGenerator = setPropertyBuulder.GetILGenerator();
			setPropGenerator.Emit(OpCodes.Ldarg_0);
			setPropGenerator.Emit(OpCodes.Ldarg_1);
			setPropGenerator.Emit(OpCodes.Stfld, propertyFieldBuilder);
			setPropGenerator.Emit(OpCodes.Ret);

			pBuilder.SetGetMethod(getPropertyBuilder);
			pBuilder.SetSetMethod(setPropertyBuulder);
		}

		private void createMethodOverride(TypeBuilder typeBuilder, MethodInfo methodOriginal, Type[] paramTypes, string hash)
		{
			MethodBuilder methodOverride = typeBuilder.DefineMethod(methodOriginal.Name,
				MethodAttributes.Public |
				MethodAttributes.SpecialName |
				MethodAttributes.RTSpecialName |
				MethodAttributes.Virtual |
				MethodAttributes.ReuseSlot |
				MethodAttributes.HideBySig,
				CallingConventions.Standard,
				methodOriginal.ReturnType,
				paramTypes);

			typeBuilder.DefineMethodOverride(methodOverride, methodOriginal);

			ILGenerator il = methodOverride.GetILGenerator();
			//definiranje dužine array parametara
			il.Emit(OpCodes.Ldc_I4, paramTypes.Length);
			//instanciranje arraya parametara
			il.Emit(OpCodes.Newarr, typeOfObject);
			//dodjeljivanje instance arraya parametara varijabli
			LocalBuilder varParameters = il.DeclareLocal(typeOfObjectArray);
			il.Emit(OpCodes.Stloc, varParameters);
			var byRefParameters = new List<KeyValuePair<int, Type>>();
			for (int i = 0; i < paramTypes.Length; i++)
			{
				//load variable parametara
				il.Emit(OpCodes.Ldloc, varParameters);
				//odabiranje indexa arraya param.
				il.Emit(OpCodes.Ldc_I4, i);
				//load parametra metode
				il.Emit(OpCodes.Ldarg, i + 1);
				Type paramType = paramTypes[i];
				if (paramType.IsByRef)
				{
					paramType = paramType.GetElementType();
					byRefParameters.Add(new KeyValuePair<int, Type>(i, paramType));
					il.Emit(OpCodes.Ldobj, paramType);
				}
				//ako je vrijednost parametra ValueType
				if (paramType.IsValueType)
					il.Emit(OpCodes.Box, paramType);
				//dodjeljivanje vrijednosti parametra metode arrayu parametara
				il.Emit(OpCodes.Stelem_Ref);
			}

			//postavljanje parametara za Cached metodu
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldstr, hash);
			il.Emit(OpCodes.Ldloc, varParameters);
			il.Emit(OpCodes.Call, getCachedMethod);
			if (methodOriginal.ReturnType == typeOfVoid)
				il.Emit(OpCodes.Pop);
			else if (methodOriginal.ReturnType.IsValueType)
				il.Emit(OpCodes.Unbox_Any, methodOriginal.ReturnType);

			//postavlja vrijednosti na ref/out parametre iz array parametara (varParameters) koji su se slali u CachedMethod
			foreach (var param in byRefParameters)
			{
				il.Emit(OpCodes.Ldarg, param.Key + 1);
				il.Emit(OpCodes.Ldloc, varParameters);
				il.Emit(OpCodes.Ldc_I4, param.Key);
				il.Emit(OpCodes.Ldelem_Ref);
				if (param.Value.IsValueType)
					il.Emit(OpCodes.Unbox_Any, param.Value);
				il.Emit(OpCodes.Stobj, param.Value);
			}

			il.Emit(OpCodes.Ret);
		}

		private DerivedTypeCache getDerivedType(Type baseType)
		{
			return typeDerivedMapping.ConditionalReadWrite(
				 tdm => !tdm.ContainsKey(baseType),
				 tdm => tdm[baseType],
				 tdm =>
				 {
					 var DTC = getDerivedTypeCache(baseType);
					 tdm.Add(baseType, DTC);
					 return DTC;
				 });
		}

		private DerivedTypeCache getDerivedTypeCache(Type baseType)
		{
			var filteredMethods = getFilteredMethods(baseType);
			var typeHash = generateHash(baseType, filteredMethods);

			return hashDerivedTypeMapping.ConditionalReadWrite(
				hdtm => !hdtm.ContainsKey(typeHash),
				hdtm => hdtm[typeHash],
				hdtm =>
				{
					List<Tuple<string, DerivedMethodCache>> derivedMethodCaches;
					var dtc = createDerivedType(baseType, filteredMethods, typeHash, out derivedMethodCaches);
					hdtm.Add(typeHash, dtc);
					updateMethodsMapping(derivedMethodCaches);
					return dtc;
				});
		}

		private IEnumerable<MethodInfo> getFilteredMethods(Type baseType)
		{
			var compatibleMethods = GetCompatibleMethods(baseType);
			return FilterMethodsByConfiguration(baseType, compatibleMethods);
		}

		private DerivedTypeCache createDerivedType(Type baseType, IEnumerable<MethodInfo> filteredMethods, string typeHash, out List<Tuple<string, DerivedMethodCache>> derivedMethodCaches)
		{
			TypeBuilder typeBuilder = createType(modBuilder, string.Concat(baseType.Name, "_CACHED_", Guid.NewGuid().ToString().Replace("-", "")), baseType);
			createConstructor(typeBuilder, baseType);

			DerivedTypeCache dtc;
			derivedMethodCaches = new List<Tuple<string, DerivedMethodCache>>();
			var callBaseMethodsRaw = new List<Tuple<string, MethodInfo, string, Type[]>>();
			if (filteredMethods.Count() > 0)
			{
				foreach (MethodInfo mi in filteredMethods)
				{
					ParameterInfo[] parameters = mi.GetParameters();
					Type[] paramTypes = new Type[parameters.Length];
					for (int i = 0; i < parameters.Length; i++)
						paramTypes[i] = parameters[i].ParameterType;

					var methodHash = generateHash(typeHash, mi);
					createMethodOverride(typeBuilder, mi, paramTypes, methodHash);
					string callBaseName = createMethodCallBase(typeBuilder, mi, paramTypes);
					callBaseMethodsRaw.Add(Tuple.Create(methodHash, mi, callBaseName, paramTypes));
				}

				var derivedType = typeBuilder.CreateType();
				dtc = new DerivedTypeCache(derivedType, baseType);
				foreach (var cbm in callBaseMethodsRaw)
					derivedMethodCaches.Add(Tuple.Create(cbm.Item1, new DerivedMethodCache
					{
						DerivedTypeCache = dtc,
						BaseMethod = cbm.Item2,
						CallBaseMethod = derivedType.GetMethod(cbm.Item3, BindingFlags.NonPublic | BindingFlags.Instance),
						ParameterTypes = cbm.Item4
					}));
			}
			else
				dtc = new DerivedTypeCache(baseType, baseType);

			return dtc;
		}

		private void updateMethodsMapping(List<Tuple<string, DerivedMethodCache>> callBaseMethods)
		{
			hashMethodMapping.Write(mm =>
			{
				foreach (var mtd in callBaseMethods)
					mm.Add(mtd.Item1, mtd.Item2);
			});
		}

		private string generateHash(Type type, IEnumerable<MethodInfo> methods)
		{
			var sb = new StringBuilder();
			sb.Append(type.FullName);
			sb.Append("-");
			foreach (var mtd in methods)
			{
				sb.Append(mtd.MethodHandle);
				sb.Append("-");
			}

			return cryptography.GetMurmur3Hash(sb.ToString());
		}

		private string generateHash(string typeHash, MethodInfo method)
		{
			var sb = new StringBuilder();
			sb.Append(typeHash);
			sb.Append("-");
			sb.Append(method.MethodHandle.Value);

			return cryptography.GetMurmur3Hash(sb.ToString());
		}

		internal List<CacheTypeConfiguration> GetTypeConfigurations(Type type)
		{
			var res = new List<CacheTypeConfiguration>();
			Configurations.Read(cr =>
			{
				foreach (CacheTypeConfiguration ctc in cr.Value)
				{
					if (ctc.Type == type || (ctc.AffectsDerivedTypesProp && ctc.Type.IsAssignableFrom(type)))
						res.Add(ctc);
				}
			});
			return res;
		}

		internal IEnumerable<MethodInfo> FilterMethodsByConfiguration(Type type, IEnumerable<MethodInfo> methods)
		{
			var filteredMethods = new Dictionary<MethodInfo, bool>();
			var cfgs = GetConfigurationsMap(type, methods);

			if (cfgs.Count > 0)
			{
				foreach (var cfg in cfgs)
				{
					foreach (var mc in cfg.Value)
					{
						filteredMethods[mc.Key] =
							mc.Value != null ? !mc.Value.Excluded
							: cfg.Key.All.HasValue ? cfg.Key.All.Value
							: filteredMethods.ContainsKey(mc.Key) ? filteredMethods[mc.Key]
							: true;
					}
				}

				return filteredMethods.Where(m => m.Value).Select(m => m.Key);
			}

			return methods;
		}

		internal Dictionary<CacheTypeConfiguration, Dictionary<MethodInfo, CacheMethodConfiguration>> GetConfigurationsMap(Type type, IEnumerable<MethodInfo> methods, IEnumerable<CacheTypeConfiguration> configs = null)
		{
			var configurationsMap = new Dictionary<CacheTypeConfiguration, Dictionary<MethodInfo, CacheMethodConfiguration>>();
			if (configs == null)
				configs = GetTypeConfigurations(type);

			Dictionary<Type, InterfaceMapping> iMaps = null;
			Dictionary<MethodInfo, MethodInfo> baseMethods = null;

			foreach (var config in configs)
			{
				var cMap = new Dictionary<MethodInfo, CacheMethodConfiguration>();
				configurationsMap[config] = cMap;

				if (config.Type.IsInterface)
				{
					if (iMaps == null) iMaps = new Dictionary<Type, InterfaceMapping>();

					foreach (var mi in methods)
					{
						if (!config.OnlyTypeMembersProp)
							cMap[mi] = null;

						foreach (var inf in config.Interfaces)
						{
							InterfaceMapping iMap;
							if (!iMaps.ContainsKey(inf))
								iMaps[inf] = iMap = type.GetInterfaceMap(inf);
							else
								iMap = iMaps[inf];

							int i = -1;
							var tm = iMap.TargetMethods.FirstOrDefault(im => { i++; return im.MethodHandle.Value == mi.MethodHandle.Value; });
							if (tm != null)
							{
								var interfaceMethod = iMap.InterfaceMethods[i];
								cMap[mi] = config.Methods.FirstOrDefault(m => interfaceMethod.MethodHandle.Value == m.MethodInfo.MethodHandle.Value);
								break;
							}
						}
					}
				}
				else
				{
					if (baseMethods == null) baseMethods = new Dictionary<MethodInfo, MethodInfo>();
					foreach (var mi in methods)
					{
						MethodInfo baseMethod;
						if (!baseMethods.ContainsKey(mi))
							baseMethods[mi] = baseMethod = mi.GetRuntimeBaseDefinition();
						else
							baseMethod = baseMethods[mi];

						var mtdCfg = config.Methods.FirstOrDefault(m => m.MethodInfo.MethodHandle.Value == baseMethod.MethodHandle.Value);
						if (mtdCfg != null)
							cMap[mi] = mtdCfg;
						else if (!config.OnlyTypeMembersProp || config.CompatibleMethods.Contains(baseMethod))
							cMap[mi] = null;
					}
				}
			}

			return configurationsMap;
		}

		internal static bool IsMethodValid(MethodInfo mi, bool ignoreAbstract = false)
		{
			return mi.IsVirtual && (ignoreAbstract || !mi.IsAbstract) && !mi.IsFinal && !mi.IsSpecialName && (mi.ReturnType != typeOfVoid || mi.GetParameters().Any(p => p.ParameterType.IsByRef));
		}

		internal static IEnumerable<MethodInfo> GetCompatibleMethods(Type type, bool ignoreAbstract = false)
		{
			return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
					   .Where(m => IsMethodValid(m, ignoreAbstract));
		}

		private static string getParametersString(object[] parameters)
		{
			StringBuilder sb = new StringBuilder();
			foreach (object param in parameters)
			{
				if (param != null)
				{
					if (param is IChecksumProvider)
						sb.Append(((IChecksumProvider)param).Checksum).Append("_");
					else
					{
						if (param is IEnumerable && !(param is string))
						{
							foreach (object pi in (IEnumerable)param)
							{
								if (param != null)
								{
									if (pi is IChecksumProvider)
										sb.Append(((IChecksumProvider)pi).Checksum);
									else
										sb.Append(pi.ToString());
								}
								sb.Append("_");
							}
						}
						else
							sb.Append(param.ToString()).Append("_");
					}
				}
				else
					sb.Append("_");
			}

			return sb.ToString();
		}

		private static string generateKey(string methodHash, object[] parameters)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(methodHash);
			sb.Append("_");
			sb.Append(getParametersString(parameters));

			return cryptography.GetMurmur3Hash(sb.ToString());
		}

		private static CacheInfo createCacheInfo(DerivedMethodCache derivedMethodCache, string key, dynamic result, object[] parameters, Type[] parameterTypes)
		{
			CacheInfo cacheInfo = new CacheInfo();
			cacheInfo.DerivedMethodCache = derivedMethodCache;
			cacheInfo.Key = key;
			cacheInfo.Result = result;
			cacheInfo.Parameters = new Dictionary<int, object>();

			for (int i = 0; i < parameterTypes.Length; i++)
			{
				if (parameterTypes[i].IsByRef)
					cacheInfo.Parameters[i] = parameters[i];
			}

			return cacheInfo;
		}

		public static dynamic GetCached(dynamic instance, string methodHash, object[] parameters)
		{
			Cache cache = instance.__Cache__;
			string key = generateKey(methodHash, parameters);

			CacheInfo cacheInfo = null;
			dynamic result = cache.values.ConditionalReadWrite(
				v => !v.ContainsKey(key),
				v =>
				{
					cacheInfo = v[key];
					cacheInfo.FillParameters(parameters);
					return cacheInfo.Result;
				},
				v =>
				{
					Type[] parameterTypes = null;
					DerivedMethodCache dmc = hashMethodMapping.Read(mw => mw.Value[methodHash]);
					parameterTypes = dmc.ParameterTypes;
					dynamic res = dmc.CallBaseMethod.Invoke(instance, parameters);
					v[key] = cacheInfo = createCacheInfo(dmc, key, res, parameters, parameterTypes);
					return res;
				});

			ThreadPool.QueueUserWorkItem(setExpiration, Tuple.Create(cache, cacheInfo));

			return result;
		}

		private static void setExpiration(object obj)
		{
			var p = (Tuple<Cache, CacheInfo>)obj;
			var cache = p.Item1;
			var cacheInfo = p.Item2;
			var methodInfo = p.Item2.DerivedMethodCache.BaseMethod;

			valuesExpiration.Write(ve =>
			 {
				 if (cacheInfo.IsDisposed) return;
				 int? expiresAt = cache.getExpiresAt(cacheInfo, methodInfo); /*(int)((long)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds - startingSecond + 5);*/

				 if (cacheInfo.ExpiresAt != expiresAt && (cacheInfo.ExpiresAt.HasValue || expiresAt.HasValue))
				 {
					 if (cacheInfo.ExpiresAt.HasValue)
					 {
						 var cacheTimeExp = ve[cacheInfo.ExpiresAt.Value];
						 var cacheExp = cacheTimeExp[cache];
						 cacheExp.Remove(cacheInfo);
						 if (cacheExp.Count == 0)
							 cacheTimeExp.Remove(cache);
						 if (cacheTimeExp.Count == 0)
							 ve.Remove(cacheInfo.ExpiresAt.Value);
					 }
					 if (expiresAt.HasValue)
					 {
						 cacheInfo.ExpiresAt = expiresAt;
						 if (ve.ContainsKey(expiresAt.Value))
						 {
							 var cacheTimeExp = ve[expiresAt.Value];
							 if (cacheTimeExp.ContainsKey(p.Item1))
								 cacheTimeExp[cache].Add(cacheInfo);
							 else
								 cacheTimeExp[cache] = new List<CacheInfo>() { cacheInfo };
						 }
						 else
							 ve[expiresAt.Value] = new Dictionary<Cache, List<CacheInfo>>() { { cache, new List<CacheInfo>() { cacheInfo } } };
					 }
				 }
			 });
		}

		private int? getExpiresAt(CacheInfo cacheInfo, MethodInfo mi)
		{
			var configs = GetTypeConfigurations(cacheInfo.DerivedMethodCache.DerivedTypeCache.BaseType);
			if (configs.Count == 0) return null;
			var mtdConfigs = GetConfigurationsMap(cacheInfo.DerivedMethodCache.DerivedTypeCache.BaseType, new[] { mi }, configs);

			bool clearAtSet = false;
			bool clearAfterSet = false;
			bool idleReadClearTimeSet = false;
			TimeSpan? clearAt = null;
			TimeSpan? clearAfter = null;
			TimeSpan? idleReadClearTime = null;
			foreach (var mtdCfgs in mtdConfigs.Values)
			{
				foreach (var ctdCfg in mtdCfgs.Values)
				{
					if (ctdCfg == null) continue;
					if (ctdCfg.ClearAtSet)
					{
						clearAtSet = true;
						clearAt = ctdCfg.ClearAtProp;
					}
					if (ctdCfg.ClearAfterSet)
					{
						clearAfterSet = true;
						clearAfter = ctdCfg.ClearAfterProp;
					}
					if (ctdCfg.IdleReadClearTimeSet)
					{
						idleReadClearTimeSet = true;
						idleReadClearTime = ctdCfg.IdleReadClearTimeProp;
					}
				}
			}
			if (!clearAtSet)
			{
				var cfg = configs.LastOrDefault(c => c.ClearAtSet);
				if (cfg != null)
					clearAt = cfg.ClearAtProp;
			}
			if (!clearAfterSet)
			{
				var cfg = configs.LastOrDefault(c => c.ClearAfterSet);
				if (cfg != null)
					clearAfter = cfg.ClearAfterProp;
			}
			if (!idleReadClearTimeSet)
			{
				var cfg = configs.LastOrDefault(c => c.IdleReadClearTimeSet);
				if (cfg != null)
					idleReadClearTime = cfg.IdleReadClearTimeProp;
			}

			DateTime? expiresAtDate;
			if (clearAfter != null)
				expiresAtDate = DateTime.Now.AddTicks(clearAfter.Value.Ticks);
			else
				expiresAtDate = null;
			if (clearAt != null)
			{
				var now = DateTime.Now;
				var expAA = now.AddTicks(clearAt.Value.Ticks - now.TimeOfDay.Ticks);
				if (expAA < now) expAA = expAA.AddDays(1);
				if (expiresAtDate == null || expiresAtDate > expAA)
					expiresAtDate = expAA;
			}
			if (idleReadClearTime != null)
			{
				var expAt = DateTime.Now.AddTicks(idleReadClearTime.Value.Ticks);
				if (expiresAtDate == null || expiresAtDate > expAt)
					expiresAtDate = expAt;
			}

			int? expiresAt = expiresAtDate.HasValue ? (int?)((long)TimeSpan.FromTicks(expiresAtDate.Value.Ticks).TotalSeconds - startingSecond) : null;
			return expiresAt;
		}

		public T I<T>()
		{
			return I(typeof(T));
		}

		public T I<T>(Type type)
		{
			return I(type);
		}

		public dynamic I(Type type)
		{
			DerivedTypeCache derivedTypeCache = getDerivedType(type);
			return derivedTypeCache.GetInstance(this);
		}

		public void AppendClearBuffer(object result)
		{
			clearBufferResult.Write(b =>
			{
				if (!b.Contains(result))
					b.Add(result);
			});
		}

		public void AppendClearBuffer(Type cachedType, bool exactType = false)
		{
			KeyValuePair<Type, bool> ct = new KeyValuePair<Type, bool>(cachedType, exactType);
			clearBufferCachedType.Write(b =>
			{
				if (!b.Contains(ct))
					b.Add(ct);
			});
		}

		public void Clear()
		{
			clearByResult();
			clearByCachedType();
		}

		public void ClearAll()
		{
			clearBufferResult.Write(br =>
				{
					clearBufferCachedType.Write(bct =>
					{
						values.Write(c =>
						{
							valuesExpiration.Write(valExp =>
							{
								br.Clear();
								bct.Clear();
								clearValuesExpiration(valExp, c.Values);
								c.Clear();
							});
						});
					});
				});
		}

		private void clearValuesExpiration(Dictionary<int, Dictionary<Cache, List<CacheInfo>>> valuesExpiration, IEnumerable<CacheInfo> cacheInfos)
		{
			var valExpForRemove = new List<int>();
			foreach (var ve in valuesExpiration)
			{
				if (ve.Value.ContainsKey(this))
				{
					var veCacheInfos = ve.Value[this];
					foreach (var cacheInfo in cacheInfos)
						veCacheInfos.Remove(cacheInfo);

					if (veCacheInfos.Count == 0)
						ve.Value.Remove(this);
					if (ve.Value.Count == 0)
						valExpForRemove.Add(ve.Key);
				}
			}

			foreach (var ve in valExpForRemove)
				valuesExpiration.Remove(ve);
		}

		private void clearByResult()
		{
			clearBufferResult.Write(buffer =>
			{
				values.Write(cacheKeyInfo =>
				{
					valuesExpiration.Write(valExp =>
					{
						Dictionary<string, CacheInfo> cacheKeysForDelete = new Dictionary<string, CacheInfo>();
						foreach (object result in buffer)
						{
							foreach (KeyValuePair<string, CacheInfo> ci in cacheKeyInfo)
							{
								if (ci.Value.Result == result)
									cacheKeysForDelete.Add(ci.Key, ci.Value);
							}
						}

						foreach (string key in cacheKeysForDelete.Keys)
							cacheKeyInfo.Remove(key);

						clearValuesExpiration(valExp, cacheKeysForDelete.Values);

						buffer.Clear();
					});
				});
			});
		}

		private void clearByCachedType()
		{
			clearBufferCachedType.Write(bw =>
			{
				values.Write(cw =>
				{
					valuesExpiration.Write(valExp =>
					{
						Dictionary<string, CacheInfo> cacheKeysForDelete = new Dictionary<string, CacheInfo>();
						foreach (KeyValuePair<Type, bool> cachedType in bw)
						{
							if (cachedType.Value)
							{
								foreach (KeyValuePair<string, CacheInfo> ci in cw)
								{
									if (ci.Value.DerivedMethodCache.DerivedTypeCache.BaseType == cachedType.Key)
										cacheKeysForDelete.Add(ci.Key, ci.Value);
								}
							}
							else
							{
								foreach (KeyValuePair<string, CacheInfo> ci in cw)
								{
									if (cachedType.Key.IsAssignableFrom(ci.Value.DerivedMethodCache.DerivedTypeCache.BaseType))
										cacheKeysForDelete.Add(ci.Key, ci.Value);
								}
							}
						}

						foreach (string key in cacheKeysForDelete.Keys)
							cw.Remove(key);

						clearValuesExpiration(valExp, cacheKeysForDelete.Values);

						bw.Clear();
					});
				});
			});
		}
	}

	public class DerivedTypeCache
	{
		public Type DerivedType { get; set; }
		public Type BaseType { get; set; }
		public ConstructorInfo DerivedTypeConstructorInfo { get; set; }

		private DerivedTypeCache()
		{
		}

		public DerivedTypeCache(Type type, Type baseType)
			: this()
		{
			DerivedType = type;
			BaseType = baseType;
			DerivedTypeConstructorInfo = type.GetConstructor(Type.EmptyTypes);
		}

		public dynamic GetInstance(Cache cache)
		{
			dynamic instance = DerivedTypeConstructorInfo.Invoke(Type.EmptyTypes);
			try
			{
				instance.__Cache__ = cache;
			}
			catch { }
			return instance;
		}
	}

	public class DerivedMethodCache
	{
		public DerivedTypeCache DerivedTypeCache { get; set; }
		public MethodInfo BaseMethod { get; set; }
		public MethodInfo CallBaseMethod { get; set; }
		public Type[] ParameterTypes { get; set; }
	}

	public class CacheInfo : IDisposable
	{
		public string Key { get; set; }
		public object Result { get; set; }
		public Dictionary<int, object> Parameters { get; set; }
		public int? ExpiresAt { get; set; }
		public DerivedMethodCache DerivedMethodCache { get; set; }
		public bool IsDisposed { get; private set; }

		public void FillParameters(object[] parameters)
		{
			foreach (var p in Parameters)
				parameters[p.Key] = p.Value;
		}

		public void Dispose()
		{
			IsDisposed = true;
			Key = null;
			Result = null;
			Parameters = null;
			ExpiresAt = null;
			DerivedMethodCache = null;
		}
	}
}
