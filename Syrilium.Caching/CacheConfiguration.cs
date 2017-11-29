using Syrilium.CachingInterface;
using Syrilium.Common;
using Syrilium.CommonInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Syrilium.Caching
{
	public class CacheConfiguration : ICacheConfiguration
	{
		internal event OneParamReturnDelegate<ICacheTypeConfiguration, CacheTypeConfiguration> OnNewType;

		public ICacheTypeConfiguration<T> Type<T>()
		{
			var ctc = new CacheTypeConfiguration<T>();
			if (OnNewType != null) ctc = (CacheTypeConfiguration<T>)OnNewType(ctc);
			return ctc;
		}

		public ICacheMethodConfiguration<T> Method<T>(Expression<Action<T>> method)
		{
			return Type<T>().Method(method);
		}

		public ICacheMethodConfiguration<T> Method<T>(MethodInfo methodInfo)
		{
			return Type<T>().Method(methodInfo);
		}

		public ICacheMethodConfiguration<T> Method<T>(string name, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
		{
			return Type<T>().Method(name, types, bindingAttr);
		}
	}

	public abstract class CacheTypeConfiguration : ICacheTypeConfiguration
	{
		public Type Type { get; protected set; }
		internal bool AffectsDerivedTypesProp { get; set; }
		internal bool OnlyTypeMembersProp { get; set; }
		internal bool ClearAtSet { get; set; }
		internal TimeSpan? ClearAtProp { get; set; }
		internal bool ClearAfterSet { get; set; }
		internal TimeSpan? ClearAfterProp { get; set; }
		internal bool? All { get; set; }
		internal bool IdleReadClearTimeSet { get; set; }
		internal TimeSpan? IdleReadClearTimeProp { get; set; }

		internal Type[] ConstructorParamTypes { get; set; }


		internal virtual List<CacheMethodConfiguration> Methods
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		internal List<MethodInfo> CompatibleMethods { get; set; }

		internal List<Type> Interfaces { get; set; }

		internal abstract void Init();

		public CacheTypeConfiguration()
		{
			CompatibleMethods = new List<MethodInfo>();
		}
	}

	public class CacheTypeConfiguration<T> : CacheTypeConfiguration, ICacheTypeConfiguration<T>
	{
		private ReaderWriterLockWrapper<ObservableCollection<ICacheMethodConfiguration<T>>> methods { get; set; }

		private volatile bool methodsChanged;

		private List<CacheMethodConfiguration> methodsList = new List<CacheMethodConfiguration>();
		private object methodsLock = new object();
		internal override List<CacheMethodConfiguration> Methods
		{
			get
			{
				if (methodsChanged)
				{
					lock (methodsLock)
					{
						if (methodsChanged)
						{
							methodsList = new List<CacheMethodConfiguration>(methods.Read(mr => mr.Cast<CacheMethodConfiguration>()));
							methodsChanged = false;
						}
					};
				}
				return methodsList;
			}
		}

		public CacheTypeConfiguration()
		{
			this.Type = typeof(T);
			if (this.Type.IsInterface)
				AffectsDerivedTypesProp = true;
			var mtds = new ObservableCollection<ICacheMethodConfiguration<T>>();
			mtds.CollectionChanged += methods_CollectionChanged;
			methods = new ReaderWriterLockWrapper<ObservableCollection<ICacheMethodConfiguration<T>>>(mtds);
		}

		internal override void Init()
		{
			if (!Type.IsInterface)
			{
				var compatibleMethods = Cache.GetCompatibleMethods(Type, true);
				foreach (var cm in compatibleMethods)
					CompatibleMethods.Add(cm.GetRuntimeBaseDefinition());
			}
			else
			{
				Interfaces = new List<Type> { Type };
				Interfaces.AddRange(Type.GetInterfaces());
			}
		}

		public ICacheMethodConfiguration<T> Method(Expression<Action<T>> func)
		{
			return onNewMethod(new CacheMethodConfiguration<T>().Method_I(func));
		}

		public ICacheMethodConfiguration<T> Method(MethodInfo methodInfo)
		{
			return onNewMethod(new CacheMethodConfiguration<T>().Method_I(methodInfo));
		}

		public ICacheMethodConfiguration<T> Method(string name, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
		{
			return onNewMethod(new CacheMethodConfiguration<T>().Method_I(name, types, bindingAttr));
		}

		public ICacheTypeConfiguration<T> AffectsDerivedTypes(bool onlyTypeMembers = false)
		{
			AffectsDerivedTypesProp = true;
			OnlyTypeMembersProp = onlyTypeMembers;
			return this;
		}

		public ICacheTypeConfiguration<T> AffectsTypeOnly()
		{
			AffectsDerivedTypesProp = false;
			return this;
		}

		public ICacheTypeConfiguration<T> ClearAt(TimeSpan? time)
		{
			ClearAtSet = true;
			ClearAtProp = time;
			return this;
		}

		public ICacheTypeConfiguration<T> ClearAfter(TimeSpan? time)
		{
			ClearAfterSet = true;
			ClearAfterProp = time;
			return this;
		}

		public ICacheTypeConfiguration<T> IdleReadClearTime(TimeSpan? time)
		{
			IdleReadClearTimeSet = true;
			IdleReadClearTimeProp = time;
			return this;
		}

		private void methods_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			methodsChanged = true;

			if (e.OldItems != null)
			{
				foreach (CacheMethodConfiguration<T> oldItem in e.OldItems)
					oldItem.OnNewMethod -= onNewMethod;
			}
			if (e.NewItems != null)
			{
				var mtds = (ObservableCollection<ICacheMethodConfiguration<T>>)sender;
				foreach (CacheMethodConfiguration<T> newItem in e.NewItems)
				{
					if (mtds.Count(m => m.MethodInfo == newItem.MethodInfo) > 1)
					{
						mtds.Remove(newItem);
						throw new InvalidOperationException("Method already exists.");
					}
					newItem.OnNewMethod += onNewMethod;
				}
			}
		}

		private ICacheMethodConfiguration<T> onNewMethod(CacheMethodConfiguration<T> cmc)
		{
			return methods.ReadWrite(cr =>
			{
				var config = cr.Value.Find(c => c.MethodInfo == cmc.MethodInfo);
				if (config != null)
					return config;
				return cr.Write(cw =>
				{
					config = cw.Find(c => c.MethodInfo == cmc.MethodInfo);
					if (config != null)
						return config;
					cw.Add(cmc);
					return cmc;
				});
			});
		}

		public ICacheTypeConfiguration<T> Exclude()
		{
			All = false;
			return this;
		}

		public ICacheTypeConfiguration<T> Include()
		{
			All = true;
			return this;
		}
	}

	public class CacheMethodConfiguration
	{
		public MethodInfo MethodInfo { get; protected set; }
		internal bool Excluded { get; set; }
		internal bool ClearAtSet { get; set; }
		internal TimeSpan? ClearAtProp { get; set; }
		internal bool ClearAfterSet { get; set; }
		internal TimeSpan? ClearAfterProp { get; set; }
		internal bool IdleReadClearTimeSet { get; set; }
		internal TimeSpan? IdleReadClearTimeProp { get; set; }
		internal bool ParamsForKeyPropSet { get; set; }
		internal int[] ParamsForKeyProp { get; set; }
	}

	public class CacheMethodConfiguration<T> : CacheMethodConfiguration, ICacheMethodConfiguration<T>
	{
		internal event OneParamReturnDelegate<ICacheMethodConfiguration<T>, CacheMethodConfiguration<T>> OnNewMethod;

		public CacheMethodConfiguration()
		{

		}

		internal CacheMethodConfiguration(MethodInfo mi)
		{
			MethodInfo = mi;
		}

		internal CacheMethodConfiguration<T> Method_I(Expression<Action<T>> action)
		{
			if (!(action.Body is MethodCallExpression))
				throw new InvalidOperationException("Action must contain method.");

			var mi = ((MethodCallExpression)action.Body).Method;

			return Method_I(mi);
		}

		internal CacheMethodConfiguration<T> Method_I(MethodInfo mi)
		{
			var parameters = mi.GetParameters().Select(p => p.ParameterType).ToArray();
			return Method_I(mi.Name, parameters);
		}

		internal CacheMethodConfiguration<T> Method_I(string name, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
		{
			var type = typeof(T);
			var rh = new ReflectionHelper();
			var methodOnType = rh.GetMethodOnType(type, name, types, bindingAttr, true);
			if (methodOnType == null)
				throw new InvalidOperationException("Method \"" + name + "\" does not exist on \"" + type.Name + "\" type.");

			if (!Cache.IsMethodValid(methodOnType, true))
				throw new InvalidOperationException("Method \"" + methodOnType.Name + "\" has to be virtual and non internal.");

			MethodInfo = methodOnType;

			return this;
		}

		public ICacheMethodConfiguration<T> Method(Expression<Action<T>> action)
		{
			return method(new CacheMethodConfiguration<T>().Method_I(action));
		}

		public ICacheMethodConfiguration<T> Method(MethodInfo methodInfo)
		{
			return method(new CacheMethodConfiguration<T>().Method_I(methodInfo));
		}

		public ICacheMethodConfiguration<T> Method(string name, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
		{
			return method(new CacheMethodConfiguration<T>().Method_I(name, types, bindingAttr));
		}

		private ICacheMethodConfiguration<T> method(CacheMethodConfiguration<T> cmc)
		{
			if (OnNewMethod != null) cmc = (CacheMethodConfiguration<T>)OnNewMethod(cmc);
			((CacheMethodConfiguration)cmc).Excluded = false;
			return cmc;
		}

		public ICacheMethodConfiguration<T> Exclude()
		{
			base.Excluded = true;
			return this;
		}

		public ICacheMethodConfiguration<T> ClearAt(TimeSpan? time)
		{
			ClearAtSet = true;
			ClearAtProp = time;
			return this;
		}

		public ICacheMethodConfiguration<T> ClearAfter(TimeSpan? time)
		{
			ClearAfterSet = true;
			ClearAfterProp = time;
			return this;
		}

		public ICacheMethodConfiguration<T> IdleReadClearTime(TimeSpan? time)
		{
			IdleReadClearTimeSet = true;
			IdleReadClearTimeProp = time;
			return this;
		}

		public ICacheMethodConfiguration<T> ParamsForKey(bool exclude, params int[] paramIndexes)
		{
			if (exclude)
			{
				var includeParams = new List<int>();
				var paramsCount = this.MethodInfo.GetParameters().Count();
				for (int i = 0; i < paramsCount; i++)
				{
					if (paramIndexes.Contains(i))
						continue;
					includeParams.Add(i);
				}
				paramIndexes = includeParams.ToArray();
			}

			ParamsForKeyPropSet = true;
			ParamsForKeyProp = paramIndexes;
			return this;
		}
	}
}
