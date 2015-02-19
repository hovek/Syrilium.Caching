using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
	public delegate T ReturnDelegate<T>();
	public delegate void TwoParamDelegate<T1, T2>(T1 p1, T2 p2);
	public delegate TRet OneParamReturnDelegate<TRet, TParam>(TParam param);
    public delegate void OneParamDelegate<T>(T p);
}
