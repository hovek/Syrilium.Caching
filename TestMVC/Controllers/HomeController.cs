using Syrilium.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace TestMVC.Controllers
{
	public class HomeController : Controller
	{
		public ActionResult Index()
		{
			Test();
			return View();
		}

		public ActionResult About()
		{
			ViewBag.Message = "Your application description page.";

			return View();
		}

		public ActionResult Contact()
		{
			ViewBag.Message = "Your contact page.";

			return View();
		}

		public static Cache MainCache = new Cache();
		public void Test()
		{
			MainCache.Config<CacheByKey>().Method("Get");

			var cacheByKey = MainCache.I<CacheByKey>();
			cacheByKey.value = "zebra";
			var sdf = cacheByKey.Get<int>(1);
			var sdf2 = cacheByKey.Get<string>(3);
			var sdf3 = cacheByKey.Get<string>(3);

			//if configuration is not set all public and protected virtual methods are cached
			//configuration should be set before caching (app start)

			//excluding all methods on all objects, nothing is cached
			MainCache.Configure.Type<object>().Exclude().AffectsDerivedTypes()
				.ClearAfter(TimeSpan.FromSeconds(10));
			//after that we are saying that we want to cache only Mtd methods on type Test and passing dummy parameters to methods so that they can be identified,
			//we could also pass in MethodInfo or just name with optional param types
			int dummyInt;
			MainCache.Configure.Method<Test>(t => t.Mtd(0, out dummyInt)).Method(t => t.Mtd(0));
			//we could also write it this way and set expiration after 3 sec of no use to method with one param
			//repeating configurations for types or methods does not insert new configuration, but changes previously set
			//referring to method automatically includes method for caching, for exclusion you also call Exclude() method
			MainCache.Configure.Type<Test>()
				.Method(t => t.Mtd(0, out dummyInt))//.Exclude()
				.Method(t => t.Mtd(0)).IdleReadClearTime(TimeSpan.FromSeconds(3));

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

			//now test no caching config
			//no derived type is created for TestNoCache because it has no cached members
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

	public class CacheByKey
	{
		public object value;
		public virtual T Get<T>(object key)
		{
			return default(T);
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