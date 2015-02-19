# Syrilium.Caching
Seamless caching and easy implementation of any virtual method in any class.

NuGet (download/install) [Syrilium.Caching](https://www.nuget.org/packages/Syrilium.Caching)
Comments on [CodeProject](http://www.codeproject.com/Tips/877717/Syrilium-Caching)

##Introduction
Caching based on virtual methods, offers caching for methods without need to code and prepare them for caching.

##Background
Idea behind this is to enable caching for any class in project and even dll references if methods are virtual with minimal preconfiguration (keeping classes clean, not worrying too much about caching).

You can have multiple instances of cache, every instance has it's own cached values and configuration and they are sharing information about types for performance and memory, it's also thread safe.

##How it works?
You start by requesting the instance of an object for which you want caching enabled for a required type. 
```cs
var test = MainCache.I<Test>();
```
Cache creates a derived type (only first time, every other time returns just new instance) that inherits Test class and overloads all virtual methods that are not excluded by configuration. This is accomplished by MSIL, similar how entity framework inherits entities for property lazy loading and more.

Then when you call `var i = test.Mtd(1);` your method is not called directly but the one that overrides it on derived class and from that overriden method method on Cache object is called that creates a cache key based on method "id" and hash created from parameter values, then it decides should original method be called or cached value is returned if it exists for generated key.

####Inside the Cache class
After first call of `MainCache.I<Test>();` method, it gets to
```cs
public dynamic I(Type type)
{
    DerivedTypeCache derivedTypeCache = getDerivedType(type);
    return derivedTypeCache.GetInstance(this);
}
```
where it first gets or creates derived type, after that it gets instance of that derived type and sets the instance of cache on it for later usage.

Derived types are shared among all cache instances, also values that have set expiration are shared among instances.

Values that have set expiration are stored in
`private static ReaderWriterLockWrapper<Dictionary<int, Dictionary<Cache, List<CacheInfo>>>> valuesExpiration;`
int key in Dictionary represents a second in time diminished by initial second that is set on static constructor of Cache and values are stored under second that they expire on. That's why seconds doesn't need to be type long.

With that, there is single timer for all cache instances that fires every second and checks dictionary for expired values, single timer for application. It stores what second was last checked and starts checking from it to current one.

##Usage example
```cs
using Syrilium.Caching;
using System;
using System.Threading;
using System.Windows;

//any .NET application: MVC, ASP.NET, Win Forms, WPF...
namespace WpfApplication1
{
	public partial class MainWindow : Window
	{
		public static Cache MainCache = new Cache();

		public MainWindow()
		{
			InitializeComponent();

			var test = MainCache.I<Test>();
			//out and ref parameters are also used for key creation
			int io;
			var i = test.Mtd(1, out io);
			//another variable because io has value from Mtd and it would generate new key and new cache value
			int io2;
			var i2 = test.Mtd(1, out io2);

			//new value
			var i3 = test.Mtd(1);
			//same as previous
			var i4 = test.Mtd(1);

			//new value because param value changed
			var i5 = test.Mtd(2);
			//same as previous
			var i6 = test.Mtd(2);

			//even if we make new instance of Test, previous cached values are preserved
			var test2 = MainCache.I<Test>();
			int io3;
			var i7 = test2.Mtd(1, out io3);
			var i8 = test2.Mtd(1);
			var i9 = test2.Mtd(2);

			//excluding all methods on all objects, nothing is cached
			MainCache.Configure.Type<object>().Exclude().AffectsDerivedTypes();
			//after that we are saying that we want to cache only Mtd methods on type Test and passing dummy parameters to methods so that they can be identified,
			//we could also pass in MethodInfo or just name with optional param types
			MainCache.Configure.Method<Test>(t => t.Mtd(0, out io3)).Method(t => t.Mtd(0));
			//we could also write it this way and set expiration after 3 sec of no use to method with one param
			//repeating configurations for types or methods does not insert new configuration, but changes previously set
			//referring to method automatically includes method for caching, for exclusion you also call Exclude() method
			MainCache.Configure.Type<Test>()
				.Method(t => t.Mtd(0, out io3))//.Exclude()
				.Method(t => t.Mtd(0)).IdleReadClearTime(TimeSpan.FromSeconds(3));

			//now test no caching config
			var testNoCache = MainCache.I<TestNoCache>();
			var i10 = testNoCache.Mtd(1);
			//has new value
			var i11 = testNoCache.Mtd(1);

			//now test cached again (we could've used previous instance of Test, results would've been the same)
			var test3 = MainCache.I<Test>();
			var i12 = test3.Mtd(1);
			//same as previous
			var i13 = test3.Mtd(1);

			Thread.Sleep(5000);
			//new value is returned
			var i14 = test3.Mtd(1);
			//same as previous
			var i15 = test3.Mtd(1);
		}
	}


	public class Test
	{
		public static int Inc;

		public virtual int Mtd(int i)
		{
			return ++Inc;
		}

		public virtual int Mtd(int i, out int io)
		{
			return io = ++Inc;
		}
	}

	public class TestNoCache
	{
		public static int Inc;

		public virtual int Mtd(int i)
		{
			return ++Inc;
		}

		public virtual int Mtd(int i, out int io)
		{
			return io = ++Inc;
		}
	}
}
```
