using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Wiki2Git
{
    /// <summary>
    /// https://www.codeproject.com/Articles/1163664/Convert-XML-to-Csharp-Object
    /// </summary>
    public class Serializer
    {
        public T Deserialize<T>(string input) where T : class
        {
            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(typeof(T));

            using (StringReader sr = new StringReader(input))
            {
                return (T?)ser.Deserialize(sr) ?? throw new Exception();
            }
        }

        public T Deserialize<T>(Stream input) where T : class
        {
            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(typeof(T));

            return (T?)ser.Deserialize(input) ?? throw new Exception();
        }

        public string Serialize<T>(T ObjectToSerialize)
        {
            if (ObjectToSerialize == null) { throw new ArgumentNullException(nameof(ObjectToSerialize)); }
            XmlSerializer xmlSerializer = new XmlSerializer(ObjectToSerialize.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, ObjectToSerialize);
                return textWriter.ToString();
            }
        }
    }
}
