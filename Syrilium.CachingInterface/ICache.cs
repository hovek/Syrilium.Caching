﻿using System;
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
using System.Linq.Expressions;
using Syrilium.CommonInterface;

namespace Syrilium.CachingInterface
{
	public interface ICache : IDisposable
	{
		ReaderWriterLockWrapper<ObservableCollection<ICacheTypeConfiguration>> Configurations { get; }
		ICacheConfiguration Configure { get; }
		ICacheTypeConfiguration<T> Config<T>();
		/// <summary>
		/// On Clear() clear found GroupKeys.
		/// </summary>
		bool ClearGroupKeys { get; set; }
		ReaderWriterLockWrapper<Dictionary<string, List<string>>> GroupKeys { get; }
		T I<T>(params object[] parameters);
		T I<T>(Type type, params object[] parameters);
		dynamic I(Type type, params object[] parameters);
		ICache AppendClearBufferKey(string key);
		ICache AppendClearBuffer(object result);
		ICache AppendClearBuffer<T>(bool exactType = false);
		ICache AppendClearBuffer(Type cachedType, bool exactType = false);
		ICache AppendClearBuffer<T>(Expression<Action<T>> method, bool exactMethodCall = true, bool exactType = true);
		void Clear();
		void ClearAll();
		bool IsDisposed { get; }
		string GenerateKey<T>(Expression<Action<T>> mtd);
		string GenerateKey(Expression<Action> mtd);
		string GenerateKey(LambdaExpression mtd);
		void AssociateGroupKey<T>(Expression<Action<T>> mtd, string groupKey);
		void AssociateGroupKey(Expression<Action> mtd, string groupKey);
		void AssociateGroupKey(LambdaExpression mtd, string groupKey);
		void AssociateGroupKey(string key, string groupKey);
		string GetMethodHash(LambdaExpression mtd, out object[] parameters);
		string GetMethodHash(LambdaExpression mtd, out MethodInfo methodInfo, out object instance, out object[] parameters);
	}

	public interface ICacheType
	{
	}

	public interface ICacheConfiguration
	{
		ICacheTypeConfiguration<T> Type<T>();
		ICacheMethodConfiguration<T> Method<T>(Expression<Action<T>> method);
		ICacheMethodConfiguration<T> Method<T>(MethodInfo methodInfo);
		ICacheMethodConfiguration<T> Method<T>(string name, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	}

	public interface ICacheTypeConfiguration
	{
		Type Type { get; }
	}

	public interface ICacheTypeConfiguration<T> : ICacheTypeConfiguration
	{
		ICacheMethodConfiguration<T> Method(Expression<Action<T>> func);
		ICacheMethodConfiguration<T> Method(MethodInfo methodInfo);
		ICacheMethodConfiguration<T> Method(string name, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		/// <summary>
		/// Tels is to affect all derived types including this one.
		/// </summary>
		/// <param name="onlyTypeMembers">If set to true it affects only overridden members originating from this type.</param>
		/// <returns></returns>
		ICacheTypeConfiguration<T> AffectsDerivedTypes(bool onlyTypeMembers = false);
		ICacheTypeConfiguration<T> AffectsTypeOnly();
		ICacheTypeConfiguration<T> Exclude();
		ICacheTypeConfiguration<T> Include();
		ICacheTypeConfiguration<T> ClearAt(TimeSpan? time);
		ICacheTypeConfiguration<T> ClearAfter(TimeSpan? time);
		ICacheTypeConfiguration<T> IdleReadClearTime(TimeSpan? time);
	}

	public interface ICacheMethodConfiguration<T>
	{
		MethodInfo MethodInfo { get; }
		ICacheMethodConfiguration<T> Method(Expression<Action<T>> func);
		ICacheMethodConfiguration<T> Method(MethodInfo methodInfo);
		ICacheMethodConfiguration<T> Method(string name, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		ICacheMethodConfiguration<T> Exclude();
		ICacheMethodConfiguration<T> ClearAt(TimeSpan? time);
		ICacheMethodConfiguration<T> ClearAfter(TimeSpan? time);
		ICacheMethodConfiguration<T> IdleReadClearTime(TimeSpan? time);
		ICacheMethodConfiguration<T> ParamsForKey(bool exclude, params int[] paramIndexes);
	}
}
