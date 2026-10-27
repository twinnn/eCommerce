using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Common.Readers.BaseXmlReader
{
    /// <summary>
    ///     Base generic XML reader
    /// </summary>
    /// <typeparam name="T">POCO class representing XML structure</typeparam>
    public class BaseXmlReader<T> where T : class
    {
        private readonly string _url;

        public BaseXmlReader(string url)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("Url cannot be null or empty");

            _url = url;
        }

        public async IAsyncEnumerable<T> GetNodes()
        {
            var xmlSettings = new XmlReaderSettings { Async = true };

            using (var reader = XmlReader.Create(_url, xmlSettings))
            {
                while (await reader.ReadAsync())
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == typeof(T).Name)
                        yield return DeserializeOrDefault<T>(reader);
            }
        }

        public static T DeserializeOrDefault<T>(XmlReader reader)
        {
            var ser = new XmlSerializer(typeof(T));
            try
            {
                return (T)ser.Deserialize(reader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {ex.Message}");
            }

            return default;
        }
    }
}