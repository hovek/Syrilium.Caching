using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;
using System.Threading;
using System.Collections;
using Syrilium.CommonInterface;
using Syrilium.Common;
using Syrilium.CachingInterface;
using System.Linq.Expressions;

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
		private static Type typeOfICacheType;
		private static Type typeOfType;
		private static Type typeOfTypeArray;
		private static MethodInfo methodGetTypeFromHandle;
		private static MethodInfo methodGetIsValueType;
		private static Cryptography cryptography;
		private static ReflectionHelper reflectionHelper;
		private static MethodInfo getCachedMethod;
		private static ReaderWriterLockWrapper<Dictionary<string, DerivedTypeCache>> hashDerivedTypeMapping;
		private static ReaderWriterLockWrapper<Dictionary<string, DerivedMethodCache>> hashMethodMapping { get; set; }
		private static int lastClearTime;
		private static long startingSecond;
		private static System.Timers.Timer clearTimer;
		private static ReaderWriterLockWrapper<Dictionary<int, Dictionary<Cache, List<CachedValueInfo>>>> valuesExpiration;

		private ReaderWriterLockWrapper<Dictionary<string, CachedValueInfo>> values;
		private ReaderWriterLockWrapper<Dictionary<Type, DerivedTypeCache>> typeDerivedMapping;
		private ReaderWriterLockWrapper<List<object>> clearBufferResult;
		private ReaderWriterLockWrapper<List<KeyValuePair<Type, bool>>> clearBufferCachedType;
		private ReaderWriterLockWrapper<List<Tuple<Type, MethodInfo, object[], bool>>> clearBufferMethod;
		private bool inConfigurationsCollectionChanged = false;

		public ReaderWriterLockWrapper<ObservableCollection<ICacheTypeConfiguration>> Configurations { get; private set; }
		public ICacheConfiguration Configure { get; private set; }
		public ICacheTypeConfiguration<T> Config<T>()
		{
			return Configure.Type<T>();
		}

		public bool IsDisposed
		{
			get;
			private set;
		}

		static Cache()
		{
			asmBuilder = Thread.GetDomain().DefineDynamicAssembly(new AssemblyName("Syrilium.Caching"), AssemblyBuilderAccess.Run);
			modBuilder = asmBuilder.DefineDynamicModule(asmBuilder.GetName().Name, false);
			hashDerivedTypeMapping = new ReaderWriterLockWrapper<Dictionary<string, DerivedTypeCache>>();
			hashMethodMapping = new ReaderWriterLockWrapper<Dictionary<string, DerivedMethodCache>>();
			valuesExpiration = new ReaderWriterLockWrapper<Dictionary<int, Dictionary<Cache, List<CachedValueInfo>>>>();

			startingSecond = (long)Math.Floor(TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds) - 1;
			clearTimer = new System.Timers.Timer(1000);
			clearTimer.AutoReset = false;
			clearTimer.Elapsed += clearTimer_Elapsed;
			clearTimer.Start();

			typeOfCache = typeof(Cache);
			typeOfObject = typeof(object);
			typeOfObjectArray = typeof(object[]);
			typeOfVoid = typeof(void);
			typeOfICacheType = typeof(ICacheType);
			typeOfTypeArray = typeof(Type[]);
			typeOfType = typeof(Type);

			methodGetTypeFromHandle = typeOfType.GetMethod("GetTypeFromHandle");
			methodGetIsValueType = typeOfType.GetProperty("IsValueType").GetGetMethod();

			getCachedMethod = typeOfCache.GetMethod("GetCached", BindingFlags.Public | BindingFlags.Static);

			cryptography = new Cryptography();
			reflectionHelper = new ReflectionHelper();
		}

		public Cache()
		{
			values = new ReaderWriterLockWrapper<Dictionary<string, CachedValueInfo>>();
			clearBufferResult = new ReaderWriterLockWrapper<List<object>>();
			clearBufferCachedType = new ReaderWriterLockWrapper<List<KeyValuePair<Type, bool>>>();
			clearBufferMethod = new ReaderWriterLockWrapper<List<Tuple<Type, MethodInfo, object[], bool>>>();
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
						baseType,
						new[] { typeOfICacheType });

			createCacheProperty(typeBuilder);

			return typeBuilder;
		}

		private static void createConstructors(TypeBuilder typeBuilder, Type baseType)
		{
			foreach (var con in baseType.GetConstructors())
				createConstructor(typeBuilder, con);
		}

		private static void createConstructor(TypeBuilder typeBuilder, ConstructorInfo baseConstructor)
		{
			var constructorTypes = baseConstructor.GetParameters().Select(p => p.ParameterType).ToArray();
			ConstructorBuilder constructor = typeBuilder.DefineConstructor(
								MethodAttributes.Public |
								MethodAttributes.SpecialName |
								MethodAttributes.RTSpecialName,
								CallingConventions.Standard,
								constructorTypes);

			//call constructor of base object
			ILGenerator il = constructor.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			for (int i = 1; i <= constructorTypes.Length; i++)
				il.Emit(OpCodes.Ldarg, i);
			il.Emit(OpCodes.Call, baseConstructor);
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

			defineGenericParameters(methodOverride, methodOriginal);
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

		private static void defineGenericParameters(MethodBuilder methodOverride, MethodInfo methodOriginal)
		{
			Type[] genArgs = methodOriginal.GetGenericArguments();
			if (genArgs.Length > 0)
			{
				IEnumerable<string> genArgsNames = genArgs.Select(a => a.Name);
				GenericTypeParameterBuilder[] ovrGenParams = methodOverride.DefineGenericParameters(genArgsNames.ToArray());

				for (int i = 0; i < genArgs.Length; i++)
				{
					ovrGenParams[i].SetGenericParameterAttributes(genArgs[i].GenericParameterAttributes);
					var interfaceCinstraints = new List<Type>();
					foreach (var paramConstr in genArgs[i].GetGenericParameterConstraints())
					{
						if (paramConstr.IsInterface)
							interfaceCinstraints.Add(paramConstr);
						else
							ovrGenParams[i].SetBaseTypeConstraint(paramConstr);
					}
					ovrGenParams[i].SetInterfaceConstraints(interfaceCinstraints.ToArray());
				}
			}
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

			defineGenericParameters(methodOverride, methodOriginal);
			typeBuilder.DefineMethodOverride(methodOverride, methodOriginal);

			ILGenerator il = methodOverride.GetILGenerator();
			//define length of array of parameters
			il.Emit(OpCodes.Ldc_I4, paramTypes.Length);
			//creating an instance of parameters array
			il.Emit(OpCodes.Newarr, typeOfObject);
			LocalBuilder varParameters = il.DeclareLocal(typeOfObjectArray);
			//assign array instance to variable
			il.Emit(OpCodes.Stloc, varParameters);
			var byRefParameters = new List<KeyValuePair<int, Type>>();
			for (int i = 0; i < paramTypes.Length; i++)
			{
				//load variable parametara
				il.Emit(OpCodes.Ldloc, varParameters);
				//setting array index to stack
				il.Emit(OpCodes.Ldc_I4, i);
				//setting method parameter onto stack
				il.Emit(OpCodes.Ldarg, i + 1);
				Type paramType = paramTypes[i];
				if (paramType.IsByRef)
				{
					paramType = paramType.GetElementType();
					byRefParameters.Add(new KeyValuePair<int, Type>(i, paramType));
					il.Emit(OpCodes.Ldobj, paramType);
				}

				if (paramType.IsGenericParameter)
				{
					//if(paramType.IsValueType
					il.Emit(OpCodes.Ldtoken, paramType);
					il.Emit(OpCodes.Call, methodGetTypeFromHandle);
					il.Emit(OpCodes.Call, methodGetIsValueType);
					il.Emit(OpCodes.Ldc_I4_0);
					il.Emit(OpCodes.Ceq);
					Label labelParamNotValueType = il.DefineLabel();
					il.Emit(OpCodes.Brtrue, labelParamNotValueType);//)
																	//{
					il.Emit(OpCodes.Box, paramType);
					//}
					//else
					il.MarkLabel(labelParamNotValueType);
				}
				else
				{
					if (paramType.IsValueType)
						il.Emit(OpCodes.Box, paramType);
				}
				//adding parameter to array
				il.Emit(OpCodes.Stelem_Ref);
			}

			//postavljanje parametara za Cached metodu
			il.Emit(OpCodes.Ldarg_0); // Ldarg_0 = this
			il.Emit(OpCodes.Ldstr, hash);
			il.Emit(OpCodes.Ldloc, varParameters);
			//adding genericArguments param
			if (methodOriginal.IsGenericMethod)
			{
				Type[] genericArguments = methodOriginal.GetGenericArguments();
				//define length of array of parameters
				il.Emit(OpCodes.Ldc_I4, genericArguments.Length);
				//creating an instance of parameters array
				il.Emit(OpCodes.Newarr, typeOfType);
				LocalBuilder varGenericArguments = il.DeclareLocal(typeOfTypeArray);
				//assign array instance to variable
				il.Emit(OpCodes.Stloc, varGenericArguments);
				for (int i = 0; i < genericArguments.Length; i++)
				{
					//load variable parametara
					il.Emit(OpCodes.Ldloc, varGenericArguments);
					//setting array index to stack
					il.Emit(OpCodes.Ldc_I4, i);
					//setting parameter onto stack
					il.Emit(OpCodes.Ldtoken, genericArguments[i]);
					//adding parameter to array
					il.Emit(OpCodes.Stelem_Ref);
				}
				il.Emit(OpCodes.Ldloc, varGenericArguments);
			}
			else
				il.Emit(OpCodes.Ldnull);

			il.Emit(OpCodes.Call, getCachedMethod);
			if (methodOriginal.ReturnType == typeOfVoid)
				il.Emit(OpCodes.Pop);
			else
			{
				if (methodOriginal.ReturnType.IsGenericParameter)
				{
					//if(methodOriginal.ReturnType.IsValueType
					il.Emit(OpCodes.Ldtoken, methodOriginal.ReturnType);
					il.Emit(OpCodes.Call, methodGetTypeFromHandle);
					il.Emit(OpCodes.Call, methodGetIsValueType);
					il.Emit(OpCodes.Ldc_I4_0);
					il.Emit(OpCodes.Ceq);
					Label labelRetParamNotValueType = il.DefineLabel();
					il.Emit(OpCodes.Brtrue, labelRetParamNotValueType);//)
																	   //{
					il.Emit(OpCodes.Unbox_Any, methodOriginal.ReturnType);
					//}
					//else
					il.MarkLabel(labelRetParamNotValueType);
				}
				else
				{
					if (methodOriginal.ReturnType.IsValueType)
						il.Emit(OpCodes.Unbox_Any, methodOriginal.ReturnType);
				}
			}

			//filling ref/out parameters with results from CachedMethod
			foreach (var param in byRefParameters)
			{
				il.Emit(OpCodes.Ldarg, param.Key + 1);
				il.Emit(OpCodes.Ldloc, varParameters);
				il.Emit(OpCodes.Ldc_I4, param.Key);
				il.Emit(OpCodes.Ldelem_Ref);

				if (param.Value.IsGenericParameter)
				{
					//if(param.Value.IsValueType
					il.Emit(OpCodes.Ldtoken, param.Value);
					il.Emit(OpCodes.Call, methodGetTypeFromHandle);
					il.Emit(OpCodes.Call, methodGetIsValueType);
					il.Emit(OpCodes.Ldc_I4_0);
					il.Emit(OpCodes.Ceq);
					Label labelRefParamNotValueType = il.DefineLabel();
					il.Emit(OpCodes.Brtrue, labelRefParamNotValueType);//)
																	   //{
					il.Emit(OpCodes.Unbox_Any, param.Value);
					//}
					//else
					il.MarkLabel(labelRefParamNotValueType);
				}
				else
				{
					if (param.Value.IsValueType)
						il.Emit(OpCodes.Unbox_Any, param.Value);
				}
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
			var typeHash = generateHash(baseType, filteredMethods.Select(m => m.MethodInfo));

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

		private IEnumerable<MethodConfig> getFilteredMethods(Type baseType)
		{
			var compatibleMethods = GetCompatibleMethods(baseType);
			return FilterMethodsByConfiguration(baseType, compatibleMethods);
		}

		private DerivedTypeCache createDerivedType(Type baseType, IEnumerable<MethodConfig> methodsForCaching, string typeHash, out List<Tuple<string, DerivedMethodCache>> derivedMethodCaches)
		{
			DerivedTypeCache dtc;
			derivedMethodCaches = new List<Tuple<string, DerivedMethodCache>>();

			if (methodsForCaching.Count() > 0)
			{
				TypeBuilder typeBuilder = createType(modBuilder, string.Concat(baseType.Name, "_CACHED_", Guid.NewGuid().ToString().Replace("-", "")), baseType);

				createConstructors(typeBuilder, baseType);

				var callBaseMethodsRaw = new List<Tuple<string, MethodInfo, string, Type[], int[]>>();

				foreach (MethodConfig mi in methodsForCaching)
				{
					ParameterInfo[] parameters = mi.MethodInfo.GetParameters();
					Type[] paramTypes = new Type[parameters.Length];
					for (int i = 0; i < parameters.Length; i++)
						paramTypes[i] = parameters[i].ParameterType;

					var methodHash = generateHash(typeHash, mi.MethodInfo);
					createMethodOverride(typeBuilder, mi.MethodInfo, paramTypes, methodHash);
					string callBaseName = createMethodCallBase(typeBuilder, mi.MethodInfo, paramTypes);
					callBaseMethodsRaw.Add(Tuple.Create(methodHash, mi.MethodInfo, callBaseName, paramTypes, mi.ParamsForKey));
				}

				var derivedType = typeBuilder.CreateType();
				dtc = new DerivedTypeCache(derivedType, baseType, typeHash);
				foreach (var cbm in callBaseMethodsRaw)
					derivedMethodCaches.Add(Tuple.Create(cbm.Item1, new DerivedMethodCache
					{
						DerivedTypeCache = dtc,
						BaseMethod = cbm.Item2,
						CallBaseMethod = derivedType.GetMethod(cbm.Item3, BindingFlags.NonPublic | BindingFlags.Instance),
						ParameterTypes = cbm.Item4,
						ParamsForKey = cbm.Item5,
						Hash = cbm.Item1
					}));
			}
			else
				dtc = new DerivedTypeCache(baseType, baseType, typeHash);

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
			sb.Append("_");
			foreach (var mtd in methods)
			{
				sb.Append(mtd.MethodHandle.Value);
				sb.Append("_");
			}

			return cryptography.GetMurmur3Hash(sb.ToString());
		}

		private string generateHash(string typeHash, MethodInfo method)
		{
			var sb = new StringBuilder();
			sb.Append(typeHash);
			sb.Append("_");
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

		internal IEnumerable<MethodConfig> FilterMethodsByConfiguration(Type type, IEnumerable<MethodInfo> methods)
		{
			var filteredMethods = new Dictionary<MethodInfo, Tuple<IWrap<bool>, MethodConfig>>();
			var cfgs = GetConfigurationsMap(type, methods);

			if (cfgs.Count > 0)
			{
				foreach (var cfg in cfgs)
				{
					foreach (var mc in cfg.Value)
					{
						bool? included = mc.Value != null ? !mc.Value.Excluded
							: cfg.Key.All.HasValue ? (bool?)cfg.Key.All.Value : null;
						int[] paramsForKey = mc.Value != null && mc.Value.ParamsForKeyPropSet ? mc.Value.ParamsForKeyProp ?? new int[0] : null;

						if (!filteredMethods.ContainsKey(mc.Key))
						{
							MethodConfig methodConfig = new MethodConfig
							{
								MethodInfo = mc.Key,
								ParamsForKey = paramsForKey
							};
							filteredMethods[mc.Key] = new Tuple<IWrap<bool>, MethodConfig>((included ?? true).Wrap(), methodConfig);
						}
						else
						{
							Tuple<IWrap<bool>, MethodConfig> tpl = filteredMethods[mc.Key];
							tpl.Item1._ = included.HasValue ? included.Value : tpl.Item1._;
							if (paramsForKey != null)
								tpl.Item2.ParamsForKey = paramsForKey;
						}
					}
				}

				return filteredMethods.Where(m => m.Value.Item1._).Select(m => m.Value.Item2);
			}

			return methods.Select(m => new MethodConfig { MethodInfo = m });
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
			return mi.IsVirtual && !mi.IsAssembly && (ignoreAbstract || !mi.IsAbstract) && !mi.IsFinal && !mi.IsSpecialName
				/*&& (mi.ReturnType != typeOfVoid || mi.GetParameters().Any(p => p.ParameterType.IsByRef))*/;
		}

		internal static IEnumerable<MethodInfo> GetCompatibleMethods(Type type, bool ignoreAbstract = false)
		{
			return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
					   .Where(m => IsMethodValid(m, ignoreAbstract));
		}

		private static string getParametersString(object[] parameters, int[] paramIndexes)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < parameters.Length; i++)
			{
				if (paramIndexes != null && !paramIndexes.Contains(i)) continue;
				object param = parameters[i];
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

		private static string generateKey(string methodHash, object[] parameters, int[] paramIndexes)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(methodHash);
			sb.Append("_");
			sb.Append(getParametersString(parameters, paramIndexes));

			return cryptography.GetMurmur3Hash(sb.ToString());
		}

		private static string generateKey(string methodHash, object[] parameters, int[] paramIndexes, MethodInfo callBaseMethod)
		{
			Type[] genericArguments;
			if (callBaseMethod.IsGenericMethod)
				genericArguments = callBaseMethod.GetGenericArguments();
			else
				genericArguments = null;

			return generateKey(methodHash, parameters, paramIndexes, ref callBaseMethod, genericArguments);
		}

		private static string generateKey(string methodHash, object[] parameters, int[] paramIndexes, ref MethodInfo callBaseMethod, Type[] genericArguments = null)
		{
			if (genericArguments != null)
			{
				callBaseMethod = callBaseMethod.MakeGenericMethod(genericArguments);
				methodHash = string.Concat(methodHash, "_", callBaseMethod.MethodHandle.Value);
			}
			return generateKey(methodHash, parameters, paramIndexes);
		}

		private static CachedValueInfo createCachedValueInfo(DerivedMethodCache derivedMethodCache, string key, dynamic result, object[] parameters, Type[] parameterTypes)
		{
			CachedValueInfo cacheInfo = new CachedValueInfo();
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

		public static dynamic GetCached(dynamic instance, string methodHash, object[] parameters, Type[] genericArguments = null)
		{
			Cache cache = instance.__Cache__;
			DerivedMethodCache dmc = hashMethodMapping.Read(mw => mw.Value[methodHash]);
			MethodInfo callBaseMethod = dmc.CallBaseMethod;
			string key = generateKey(methodHash, parameters, dmc.ParamsForKey, ref callBaseMethod, genericArguments);

			CachedValueInfo cacheInfo = null;
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
					dynamic res = callBaseMethod.Invoke(instance, parameters);
					v[key] = cacheInfo = createCachedValueInfo(dmc, key, res, parameters, dmc.ParameterTypes);
					return res;
				});

			ThreadPool.QueueUserWorkItem(setExpiration, Tuple.Create(cache, cacheInfo));

			return result;
		}

		private static void setExpiration(object obj)
		{
			valuesExpiration.Write(ve =>
			 {
				 var p = (Tuple<Cache, CachedValueInfo>)obj;
				 var cacheInfo = p.Item2;
				 if (cacheInfo.IsDisposed) return;
				 var cache = p.Item1;
				 var methodInfo = p.Item2.DerivedMethodCache.BaseMethod;

				 int? expiresAt = cache.getExpiresAt(cacheInfo, methodInfo); /*(int)((long)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds - startingSecond + 5);*/

				 if (cacheInfo.ExpiresAt != expiresAt)
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
								 cacheTimeExp[cache] = new List<CachedValueInfo>() { cacheInfo };
						 }
						 else
							 ve[expiresAt.Value] = new Dictionary<Cache, List<CachedValueInfo>>() { { cache, new List<CachedValueInfo>() { cacheInfo } } };
					 }
				 }
			 });
		}

		private int? getExpiresAt(CachedValueInfo cacheInfo, MethodInfo mi)
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

			var now = DateTime.Now;
			DateTime? expiresAtDate;
			if (clearAfter != null)
				expiresAtDate = cacheInfo.Created.Value.AddTicks(clearAfter.Value.Ticks);
			else
				expiresAtDate = null;
			if (clearAt != null)
			{
				var expAA = now.AddTicks(clearAt.Value.Ticks - now.TimeOfDay.Ticks);
				if (expAA < now) expAA = expAA.AddDays(1);
				if (expiresAtDate == null || expiresAtDate > expAA)
					expiresAtDate = expAA;
			}
			if (idleReadClearTime != null)
			{
				var expAt = now.AddTicks(idleReadClearTime.Value.Ticks);
				if (expiresAtDate == null || expiresAtDate > expAt)
					expiresAtDate = expAt;
			}

			int? expiresAt = expiresAtDate.HasValue ? (int?)((long)TimeSpan.FromTicks(expiresAtDate.Value.Ticks).TotalSeconds - startingSecond) : null;
			return expiresAt;
		}

		public T I<T>(params object[] parameters)
		{
			return I(typeof(T), parameters);
		}

		public T I<T>(Type type, params object[] parameters)
		{
			return I(type, parameters);
		}

		public dynamic I(Type type, params object[] parameters)
		{
			DerivedTypeCache derivedTypeCache = getDerivedType(type);
			return derivedTypeCache.GetInstance(this, parameters);
		}

		public ICache AppendClearBuffer(object result)
		{
			clearBufferResult.Write(b =>
			{
				if (!b.Contains(result))
					b.Add(result);
			});

			return this;
		}

		public ICache AppendClearBuffer<T>(bool exactType = false)
		{
			return AppendClearBuffer(typeof(T), exactType);
		}

		public ICache AppendClearBuffer(Type cachedType, bool exactType = false)
		{
			KeyValuePair<Type, bool> ct = new KeyValuePair<Type, bool>(cachedType, exactType);
			clearBufferCachedType.Write(b =>
			{
				if (!b.Contains(ct))
					b.Add(ct);
			});

			return this;
		}

		public ICache AppendClearBuffer<T>(Expression<Action<T>> action, bool exactMethodCall = true, bool exactType = true)
		{
			if (!(action.Body is MethodCallExpression))
				throw new InvalidOperationException("Action must contain method.");

			var mce = ((MethodCallExpression)action.Body);
			var parameters = exactMethodCall ? mce.Arguments.Select(a => ((ConstantExpression)a).Value).ToArray() : null;

			clearBufferMethod.Write(cw =>
			{
				cw.Add(new Tuple<Type, MethodInfo, object[], bool>(typeof(T), mce.Method, parameters, exactType));
			});

			return this;
		}

		public void Clear()
		{
			clearByResult();
			clearByCachedType();
			clearByMethod();
		}

		public void ClearAll()
		{
			ReadWriteLock.Lock(new[] { clearBufferResult.W, clearBufferCachedType.W, clearBufferMethod.W, values.W, valuesExpiration.W }, () =>
			{
				clearBufferResult.Value.Clear();
				clearBufferCachedType.Value.Clear();
				clearBufferMethod.Value.Clear();
				clearValuesExpiration(valuesExpiration.Value, values.Value.Values);
				foreach (var cacheInfo in values.Value.Values)
					cacheInfo.Dispose();
				values.Value.Clear();
			});
		}

		private void clearValuesExpiration(Dictionary<int, Dictionary<Cache, List<CachedValueInfo>>> valuesExpiration, IEnumerable<CachedValueInfo> cacheInfos)
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
			ReadWriteLock.Lock(new[] { clearBufferResult.W, values.W, valuesExpiration.W }, () =>
			{
				Dictionary<string, CachedValueInfo> cacheKeysForDelete = new Dictionary<string, CachedValueInfo>();
				foreach (object result in clearBufferResult.Value)
				{
					foreach (KeyValuePair<string, CachedValueInfo> ci in values.Value)
					{
						if (ci.Value.Result == result)
						{
							ci.Value.Dispose();
							cacheKeysForDelete.Add(ci.Key, ci.Value);
						}
					}
				}

				foreach (string key in cacheKeysForDelete.Keys)
					values.Value.Remove(key);

				clearValuesExpiration(valuesExpiration.Value, cacheKeysForDelete.Values);

				clearBufferResult.Value.Clear();
			});
		}

		private void clearByCachedType()
		{
			ReadWriteLock.Lock(new[] { clearBufferCachedType.W, values.W, valuesExpiration.W }, () =>
			{
				Dictionary<string, CachedValueInfo> cacheKeysForDelete = new Dictionary<string, CachedValueInfo>();
				foreach (KeyValuePair<Type, bool> cachedType in clearBufferCachedType.Value)
				{
					if (cachedType.Value)
					{
						foreach (KeyValuePair<string, CachedValueInfo> ci in values.Value)
						{
							if (ci.Value.DerivedMethodCache.DerivedTypeCache.BaseType == cachedType.Key)
							{
								ci.Value.Dispose();
								cacheKeysForDelete.Add(ci.Key, ci.Value);
							}
						}
					}
					else
					{
						foreach (KeyValuePair<string, CachedValueInfo> ci in values.Value)
						{
							if (cachedType.Key.IsAssignableFrom(ci.Value.DerivedMethodCache.DerivedTypeCache.BaseType))
							{
								ci.Value.Dispose();
								cacheKeysForDelete.Add(ci.Key, ci.Value);
							}
						}
					}
				}

				foreach (string key in cacheKeysForDelete.Keys)
					values.Value.Remove(key);

				clearValuesExpiration(valuesExpiration.Value, cacheKeysForDelete.Values);

				clearBufferCachedType.Value.Clear();
			});
		}

		private void clearByMethod()
		{
			ReadWriteLock.Lock(new[] { clearBufferMethod.W, values.W, valuesExpiration.W, hashMethodMapping.R }, () =>
			{
				Dictionary<string, CachedValueInfo> cacheKeysForDelete = new Dictionary<string, CachedValueInfo>();
				foreach (var bufferItem in clearBufferMethod.Value)
				{
					var derivedMethodsCache = new Dictionary<DerivedMethodCache, string>();

					foreach (var dmc in hashMethodMapping.Value.Values)
					{
						if (dmc.BaseMethod == bufferItem.Item2
							&& (!bufferItem.Item4 || dmc.DerivedTypeCache.BaseType == bufferItem.Item1))
						{
							string key = null;
							if (bufferItem.Item3 != null)
								key = generateKey(dmc.Hash, bufferItem.Item3, dmc.ParamsForKey, dmc.CallBaseMethod);
							derivedMethodsCache[dmc] = key;
						}
					}

					foreach (var dmc in derivedMethodsCache)
					{
						foreach (KeyValuePair<string, CachedValueInfo> ci in values.Value)
						{
							if (ci.Value.DerivedMethodCache == dmc.Key && (dmc.Value == null || ci.Key == dmc.Value))
							{
								ci.Value.Dispose();
								cacheKeysForDelete.Add(ci.Key, ci.Value);
							}
						}
					}
				}

				foreach (string key in cacheKeysForDelete.Keys)
					values.Value.Remove(key);

				clearValuesExpiration(valuesExpiration.Value, cacheKeysForDelete.Values);

				clearBufferMethod.Value.Clear();
			});
		}

		public void Dispose()
		{
			ClearAll();
			values = null;
			clearBufferResult = null;
			clearBufferCachedType = null;
			clearBufferMethod = null;
			Configurations.Write(c => c.Clear());
			Configurations = null;
			typeDerivedMapping.Write(tdm => tdm.Clear());
			typeDerivedMapping = null;
			IsDisposed = true;
		}
	}

	public class DerivedTypeCache
	{
		public Type DerivedType { get; set; }
		public Type BaseType { get; set; }

		public string Hash { get; set; }
		public Dictionary<ConstructorInfo, ParameterInfo[]> DerivedTypeConstructorsInfoParams { get; set; }

		public DerivedTypeCache()
		{

		}

		public DerivedTypeCache(Type type, Type baseType, string hash)
			: this()
		{
			DerivedType = type;
			BaseType = baseType;
			Hash = hash;
			DerivedTypeConstructorsInfoParams = new Dictionary<ConstructorInfo, ParameterInfo[]>();
			foreach (var con in type.GetConstructors())
				DerivedTypeConstructorsInfoParams.Add(con, con.GetParameters());
		}

		public dynamic GetInstance(Cache cache, params object[] parameters)
		{
			dynamic instance = GetConstructorInfo(parameters).Invoke(parameters);
			if (instance is ICacheType)
				instance.__Cache__ = cache;
			return instance;
		}

		public ConstructorInfo GetConstructorInfo(object[] parameters)
		{
			ConstructorInfo ctorRet = null;
			int lastParamCntMatch = 0;
			foreach (var ctor in DerivedTypeConstructorsInfoParams)
			{
				if (ctor.Value.Length < parameters.Length || ctor.Value.Length < lastParamCntMatch) continue;

				int i = 0;
				for (; i < parameters.Length; i++)
				{
					if (ctor.Value[i].ParameterType.IsInstanceOfType(parameters[i]))
						continue;
					else if (parameters[i] == null
						&& ctor.Value[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(ctor.Value[i].ParameterType) == null)
						break;
				}

				if (lastParamCntMatch < i)
				{
					ctorRet = ctor.Key;
					if (i == parameters.Length) break;
					lastParamCntMatch = i;
				}
			}

			return ctorRet;
		}
	}

	public class DerivedMethodCache
	{
		public DerivedTypeCache DerivedTypeCache { get; set; }
		public MethodInfo BaseMethod { get; set; }
		public MethodInfo CallBaseMethod { get; set; }
		public Type[] ParameterTypes { get; set; }
		public int[] ParamsForKey { get; set; }
		public string Hash { get; set; }
	}

	public class CachedValueInfo : IDisposable
	{
		public string Key { get; set; }
		public object Result { get; set; }
		public Dictionary<int, object> Parameters { get; set; }
		public int? ExpiresAt { get; set; }
		public DerivedMethodCache DerivedMethodCache { get; set; }
		public DateTime? Created { get; set; }
		public bool IsDisposed { get; private set; }

		public CachedValueInfo()
		{
			Created = DateTime.Now;
		}

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
			Created = null;
		}
	}

	public class MethodConfig
	{
		public MethodInfo MethodInfo;
		public int[] ParamsForKey;
	}
}
