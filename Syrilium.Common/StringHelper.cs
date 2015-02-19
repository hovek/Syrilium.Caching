using Syrilium.CommonInterface;
using System.Collections.Generic;
using System.Text;

namespace Syrilium.Common
{
    public class StringHelper : IStringHelper
    {
        public string ExtractQuotedText(string rawInput, string delimiterPrefix, string delimiterSufix, out List<string> quotedText)
        {
            quotedText = new List<string>();
            StringBuilder condition = new StringBuilder();
            bool isOpen = false;
            int curr = -1;
            int prev;
            int from = 0;
            int to = 0;
            while (true)
            {
                prev = curr;
                curr = rawInput.IndexOf('"', curr + 1);
                if (curr > -1)
                {
                    if (isOpen && rawInput.Length > curr + 1 && rawInput.Substring(curr + 1, 1).Equals(@""""))
                    {
                        curr++;
                        continue;
                    }
                    else
                    {
                        isOpen = !isOpen;
                    }
                    if (prev == -1)
                    {
                        to = curr;
                    }
                }

                if (curr == -1 || (isOpen && prev != -1 && curr - prev > 1))
                {
                    if (prev > -1)
                    {
                        condition.Append(rawInput.Substring(from, to - from));
                        string value = rawInput.Substring(to + 1, prev - to - 1);
                        quotedText.Add(value);
                        string valueId = string.Concat(delimiterPrefix, (quotedText.Count - 1).ToString(), delimiterSufix);
                        condition.Append(valueId);
                    }

                    if (curr != -1)
                    {
                        from = prev + 1;
                        to = curr;
                    }
                    else
                    {
                        condition.Append(rawInput.Substring(prev + 1, rawInput.Length - prev - 1));
                        break;
                    }
                }
            }

            return condition.ToString();
        }
    }
}
