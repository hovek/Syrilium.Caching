using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
    public interface IStringHelper
    {
        string ExtractQuotedText(string rawInput, string delimiterPrefix, string delimiterSufix, out List<string> quotedText);
    }
}
