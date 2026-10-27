using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;

namespace Common.Contract.Serialization
{
    public class Utf8Serializer : ISerializer
    {
        private readonly ILogger<Utf8Serializer> _logger;

        public Utf8Serializer(ILogger<Utf8Serializer> logger)
        {
            _logger = logger;
        }

        public string Serialize<T>(T data)
        {
            var x = new XmlSerializer(data?.GetType() ?? throw new ArgumentNullException());

            string s;
            try
            {
                using var writer = new Utf8StringWriter();

                x.Serialize(writer, data);
                s = writer.ToString() ?? throw new InvalidOperationException();
            }
            catch
            {
                _logger.LogError("Error while serializing {data} data type.", typeof(T));
                throw;
            }

            return s;
        }

        public T Deserialize<T>(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                _logger.LogError("Error, can't deserialize empty value.");
                return default(T) ?? throw new ArgumentException();
            }

            ;

            var serializer = new XmlSerializer(typeof(T));

            var settings = new XmlReaderSettings();

            using var textReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(textReader, settings);
            T data;
            try
            {
                data = (T)serializer.Deserialize(xmlReader)! ?? throw new InvalidOperationException();
            }
            catch
            {
                _logger.LogError("Error while deserializing {data} data type.", typeof(T));
                throw;
            }

            return data;
        }
    }
}