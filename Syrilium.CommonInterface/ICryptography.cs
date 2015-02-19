using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace Syrilium.CommonInterface
{
    public interface ICryptography
    {
        string GetMD5Hash(string text);
        string GetMurmur3Hash(string text);
    }
}
