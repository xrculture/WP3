using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace XRCultureRegisterTool
{
    public class SignalRLogClient
    {
        private string _serverUrl;
        private HubConnection? _logConnection = null;
        private HubConnection? _statusConnection = null;
        private System.Windows.Forms.TextBox _logTextBox;

        public SignalRLogClient(string serverUrl, System.Windows.Forms.TextBox logTextBox)
        {
            _serverUrl = serverUrl;
            _logTextBox = logTextBox;
        }

        public async Task InitializeSignalRLogHubConnectionAsync()
        {
            _logConnection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}logHub")
                .Build();

            _logConnection.On<string>("ReceiveLogMessage", (message) =>
            {
                if (_logTextBox.InvokeRequired)
                {
                    _logTextBox.Invoke(new Action(() =>
                    {
                        _logTextBox.AppendText(message + Environment.NewLine);
                        _logTextBox.SelectionStart = _logTextBox.Text.Length;
                        _logTextBox.ScrollToCaret();
                    }));
                }
                else
                {
                    _logTextBox.AppendText(message + Environment.NewLine);
                    _logTextBox.SelectionStart = _logTextBox.Text.Length;
                    _logTextBox.ScrollToCaret();
                }
            });

            try
            {
                await _logConnection.StartAsync();
                await _logConnection.InvokeAsync("JoinGroup", "***SignalR-Log-Hub***");

                _logTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - Connected to SignalR log hub" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _logTextBox.AppendText($"Connection failed: {ex.Message}" + Environment.NewLine);
            }
        }

        public async Task InitializeSignalRStatusHubConnectionAsync()
        {
            _statusConnection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}statusHub")
                .Build();

            _statusConnection.On<string>("ReceiveStatusUpdate", (message) =>
            {
                if (_logTextBox.InvokeRequired)
                {
                    _logTextBox.Invoke(new Action(() =>
                    {
                        _logTextBox.AppendText(message + Environment.NewLine);
                        _logTextBox.SelectionStart = _logTextBox.Text.Length;
                        _logTextBox.ScrollToCaret();
                    }));
                }
                else
                {
                    _logTextBox.AppendText(message + Environment.NewLine);
                    _logTextBox.SelectionStart = _logTextBox.Text.Length;
                    _logTextBox.ScrollToCaret();
                }

                dynamic jsonResponse = JsonConvert.DeserializeObject(message);
                if ((jsonResponse?.taskStatusUpdate != null) &&
                    (jsonResponse?.taskStatusUpdate.Status == "Completed"))
                {
                    var viewUrl = jsonResponse?.taskStatusUpdate.ViewUrl;
                    var downloadUrl = jsonResponse?.taskStatusUpdate.DownloadUrl;

                    try
                    {
                        string modelLoadingRequest = 
                            @"<?xml version=""1.0"" encoding=""UTF-8""?>
                                <ModelLoadingRequest>
	                            <ServiceID>0002</ServiceID>
	                            <SessionToken>e3be7cc2-3a7e-45e6-9a88-bd364e6de740</SessionToken>
	                            <Source>
		                            <UrlSource>
			                            <FileExtension>.binz</FileExtension>
			                            <FileDimension>-1</FileDimension>
			                            <Url>%URL%</Url>
		                            </UrlSource>
	                            </Source>
	                            <SceneInit>
		                            <Zoom default=""True""/>
		                            <Pan default=""True""/>
		                            <BackgroundColor default=""True""/>
		                            <View default=""True""/>
		                            <Lights default=""True""/>
	                            </SceneInit>
                            </ModelLoadingRequest>".Replace("%URL%", Uri.EscapeDataString(_serverUrl.TrimEnd('/') + (string?)downloadUrl));

                        using (HttpClient client = new HttpClient())
                        {
                            var url = "http://xrcultureviewer.rdf.bg:30026/Viewer";
                            //var url = "https://localhost:6131/Viewer"; // Localhost for testing
                            var content = new StringContent(JsonConvert.SerializeObject(modelLoadingRequest, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8, "application/xml");

                            HttpResponseMessage response = client.PostAsync(url, content).Result;
                            string responseString = response.Content.ReadAsStringAsync().Result;
                            Console.WriteLine(responseString);

                            if (_logTextBox.InvokeRequired)
                            {
                                _logTextBox.Invoke(new Action(() =>
                                {
                                    _logTextBox.AppendText(responseString + Environment.NewLine);
                                    _logTextBox.SelectionStart = _logTextBox.Text.Length;
                                    _logTextBox.ScrollToCaret();
                                }));
                            }
                            else
                            {
                                _logTextBox.AppendText(responseString + Environment.NewLine);
                                _logTextBox.SelectionStart = _logTextBox.Text.Length;
                                _logTextBox.ScrollToCaret();
                            }

                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(responseString);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}\n" +
                            $"Details:\n{ex.StackTrace}");
                    }
                }
            });

            try
            {
                await _statusConnection.StartAsync();
                await _statusConnection.InvokeAsync("JoinGroup", "***SignalR-Status-Hub***");

                _logTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - Connected to SignalR status hub" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _logTextBox.AppendText($"Connection failed: {ex.Message}" + Environment.NewLine);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_logConnection != null)
            {
                await _logConnection.DisposeAsync();
            }

            if (_statusConnection != null)
            {
                await _statusConnection.DisposeAsync();
            }
        }
    }
}
