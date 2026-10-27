using System.IO;
using System.Text;

namespace Common.Contract.Serialization
{
    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}