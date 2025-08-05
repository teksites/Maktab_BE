using System.Xml.Serialization;
using System.Xml;
using System.IO;

namespace Elavon.Implementation
{
    public class MyXmlSerializer<T> where T : class
    {
        public static string Serialize(T obj)
        {
            XmlSerializer xsSubmit = new XmlSerializer(typeof(T));
          //  using (var sww = new StringWriter())
            using (var sww = new MemoryStream())
            {
                //using (XmlTextWriter writer = new XmlTextWriter(sww) { Formatting = Formatting.Indented })
                using (XmlTextWriter writer = new XmlTextWriter(sww, System.Text.Encoding.UTF8) { Formatting = Formatting.Indented })
                {
                    xsSubmit.Serialize(writer, obj);
                    sww.Position = 0;
                    StreamReader reader = new StreamReader(sww);
                    var ss = reader.ReadLine();
                    ss =reader.ReadLine();
                    string text = reader.ReadToEnd();
                    return "<txn>"+text;
                }
            }
        }
    }
}
