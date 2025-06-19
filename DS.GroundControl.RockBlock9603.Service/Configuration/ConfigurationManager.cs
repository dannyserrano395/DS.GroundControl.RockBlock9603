using System.Net;
using System.Xml;
using System.Reflection;

namespace DS.GroundControl.RockBlock9603.Service.Configuration
{
    public class ConfigurationManager : IConfigurationManager
    {
        public ServiceConfiguration ServiceConfiguration { get; private set; }
        public WorkerConfiguration WorkerConfiguration { get; private set; }

        public ConfigurationManager()
        {
            ServiceConfiguration = new ServiceConfiguration();
            WorkerConfiguration = new WorkerConfiguration();
            Initialize();
            Validate();
        }
       
        private void Initialize()
        {
            var configurations = GetType().GetProperties();
            for (int i = 0; i < configurations.Length; i++)
            {
                Initialize(configurations[i].GetValue(this));
            }
            return;
        }
        private static void Initialize<T>(T configuration)
        {
            var properties = configuration.GetType().GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                string propertyValue = ReadConfigurationValue(properties[i]);

                if (properties[i].PropertyType == typeof(string))
                {
                    properties[i].SetValue(configuration, propertyValue);
                }
                else if (properties[i].PropertyType == typeof(int))
                {
                    var value = int.TryParse(propertyValue, out var result)
                        ? result
                        : default;

                    properties[i].SetValue(configuration, value);
                }
                else if (properties[i].PropertyType == typeof(IPEndPoint))
                {
                    var value = IPEndPoint.TryParse(propertyValue, out var result)
                        ? result
                        : default;

                    properties[i].SetValue(configuration, value);
                }
            }
            return;
        }
        private void Validate()
        {
            var exceptions = new List<Exception>();

            ValidateServiceConfiguration(exceptions);
            ValidateWorkerConfiguration(exceptions);

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
            return;
        }
        private void ValidateServiceConfiguration(List<Exception> exceptions)
        {
            if (string.IsNullOrWhiteSpace(ServiceConfiguration.ServiceName))
            {
                exceptions.Add(new Exception($"{nameof(Configuration.ServiceConfiguration)}.config contains an invalid {nameof(Configuration.ServiceConfiguration.ServiceName)} value."));
            }

            if (string.IsNullOrWhiteSpace(ServiceConfiguration.DisplayName))
            {
                exceptions.Add(new Exception($"{nameof(Configuration.ServiceConfiguration)}.config contains an invalid {nameof(Configuration.ServiceConfiguration.DisplayName)} value."));
            }

            if (string.IsNullOrWhiteSpace(ServiceConfiguration.Description))
            {
                exceptions.Add(new Exception($"{nameof(Configuration.ServiceConfiguration)}.config contains an invalid {nameof(Configuration.ServiceConfiguration.Description)} value."));
            }
        }
        private void ValidateWorkerConfiguration(List<Exception> exceptions)
        {

        }
        private static string ReadConfigurationValue(PropertyInfo property)
        {
            try
            {
                var path = string.Empty;
                var parentNode = string.Empty;

                if (property.DeclaringType.Name == nameof(Configuration.ServiceConfiguration))
                {
                    path = $"{AppContext.BaseDirectory}_config/ServiceConfiguration.config";
                    parentNode = "serviceConfiguration";
                }
                else if (property.DeclaringType.Name == nameof(Configuration.WorkerConfiguration))
                {
                    path = $"{AppContext.BaseDirectory}_config/WorkerConfiguration.config";
                    parentNode = "workerConfiguration";
                }

                var xmlDocument = new XmlDocument();
                xmlDocument.Load(path);

                var childNodes = xmlDocument[parentNode].ChildNodes;

                var nodes = childNodes.Cast<XmlNode>()
                    .Where(n =>
                    n.NodeType == XmlNodeType.Element &&
                    n.Attributes.GetNamedItem("key").Value == property.Name);

                var node = nodes.Count() == 1
                    ? nodes.First()
                    : null;

                return node.Attributes.GetNamedItem("value").Value; 
            }
            catch
            {
                return null;
            }
        }
    }
}