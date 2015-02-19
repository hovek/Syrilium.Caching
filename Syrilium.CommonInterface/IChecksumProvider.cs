using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
    public interface IChecksumProvider
    {
        string Checksum { get; }
    }
}
