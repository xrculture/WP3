using Newtonsoft.Json;
using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Policy;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace XRCultureRegisterTool
{
    public partial class RegisterViewerForm : Form
    {
        public RegisterViewerForm()
        {
            InitializeComponent();
        }

        private void RegisterViewerForm_Load(object sender, EventArgs e)
        {
            //_textBoxHubURL.Text = "http://xrculturehub.rdf.bg:30026/";
            _textBoxHubURL.Text = "http://localhost:5130/"; // Debug or Docker
        }

        private void RegisterViewerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        async private void _buttonAuthorize_Click(object sender, EventArgs e)
        {
            // Reset the session token and button state
            SessionToken = null;
            _buttonRegister.Enabled = false;
            _textBoxLog.Text = string.Empty;

            // Validate the Hub URL input
            if (string.IsNullOrWhiteSpace(_textBoxHubURL.Text))
            {
                MessageBox.Show("Please enter the Hub URL.");
                return;
            }

            // Validate the Hub URL
            if (!Uri.TryCreate(_textBoxHubURL.Text, UriKind.Absolute, out Uri? hubUri) || !hubUri.IsWellFormedOriginalString())
            {
                MessageBox.Show("Please enter a valid Hub URL.");
                return;
            }
            // Check if the scheme is either http or https
            if (!hubUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !hubUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Please enter a valid Hub URL with http or https scheme.");
                return;
            }

            var openFileDialog = new OpenFileDialog()
            {
                FileName = "XML/JSON file",
                Filter = "XML files (*.xml)|*.xml|JSON files (*.json)|*.json",
                Title = "Open JSON/XML file"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    bool bJSONContent = openFileDialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                    if (!bJSONContent && !openFileDialog.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Please select a valid XML or JSON file.");
                        return;
                    }

                    var streamReader = new StreamReader(openFileDialog.FileName);
                    var authorizeRequest = streamReader.ReadToEnd().Replace("%TIME_STAMP%", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (!bJSONContent)
                    {
                        authorizeRequest = JsonConvert.SerializeObject(authorizeRequest, Newtonsoft.Json.Formatting.Indented);
                    }

                    using (HttpClient client = new HttpClient())
                    {
                        var url = _textBoxHubURL.Text + "Registry";
                        var content = new StringContent(authorizeRequest, Encoding.UTF8,
                            bJSONContent ? "application/json" : "application/xml");

                        HttpResponseMessage response = await client.PostAsync(url, content);

                        string responseString = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrEmpty(responseString))
                        {
                            throw new Exception("Empty response from the server.");
                        }

                        _textBoxLog.Text = responseString;

                        if (bJSONContent)
                        {
                            //
                            // JSON response handling
                            //

                            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                            var status = jsonResponse?.Status;
                            if (status == "202")
                            {
                                SessionToken = (string?)jsonResponse?.SessionToken;
                                _buttonRegister.Enabled = !string.IsNullOrEmpty(SessionToken);
                            }
                            else
                            {
                                throw new Exception($"Authorization failed. Status: {status}");
                            }
                        }
                        else
                        {
                            //
                            // XML response handling
                            // 

                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(responseString);

                            var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                            if (status?.Trim() == "202")
                            {
                                SessionToken = xmlDoc.SelectSingleNode("//SessionToken")?.InnerText;
                                _buttonRegister.Enabled = !string.IsNullOrEmpty(SessionToken);
                            }
                            else
                            {
                                throw new Exception($"Authorization failed. Status: {status}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\n" +
                        $"Details:\n{ex.StackTrace}");
                }
            }
        }

        async private void _buttonRegister_Click(object sender, EventArgs e)
        {
            // Validate the Hub URL input
            if (string.IsNullOrWhiteSpace(_textBoxHubURL.Text))
            {
                MessageBox.Show("Please enter the Hub URL.");
                return;
            }

            // Validate the Hub URL
            if (!Uri.TryCreate(_textBoxHubURL.Text, UriKind.Absolute, out Uri? hubUri) || !hubUri.IsWellFormedOriginalString())
            {
                MessageBox.Show("Please enter a valid Hub URL.");
                return;
            }
            // Check if the scheme is either http or https
            if (!hubUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !hubUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Please enter a valid Hub URL with http or https scheme.");
                return;
            }

            var openFileDialog = new OpenFileDialog()
            {
                FileName = "XML/JSON file",
                Filter = "XML files (*.xml)|*.xml|JSON files (*.json)|*.json",
                Title = "Open JSON/XML file"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    bool bJSONContent = openFileDialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                    if (!bJSONContent && !openFileDialog.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Please select a valid XML or JSON file.");
                        return;
                    }

                    var streamReader = new StreamReader(openFileDialog.FileName);
                    var registerRequest = streamReader.ReadToEnd();
                    registerRequest = registerRequest.Replace("%SESSION_TOKEN%", SessionToken ?? string.Empty);
                    if (!bJSONContent)
                    {
                        registerRequest = JsonConvert.SerializeObject(registerRequest, Newtonsoft.Json.Formatting.Indented);
                    }

                    using (HttpClient client = new HttpClient())
                    {
                        var url = _textBoxHubURL.Text + "Registry";
                        var content = new StringContent(registerRequest, Encoding.UTF8,
                            bJSONContent ? "application/json" : "application/xml");

                        HttpResponseMessage response = await client.PostAsync(url, content);

                        string responseString = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrEmpty(responseString))
                        {
                            throw new Exception("Empty response from the server.");
                        }

                        _textBoxLog.Text = responseString;

                        if (bJSONContent)
                        {
                            //
                            // JSON response handling
                            //

                            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                            var status = jsonResponse?.Status;
                            if (jsonResponse?.Status == "200")
                            {
                                MessageBox.Show("Viewer successfully registered.");
                            }
                            else
                            {
                                throw new Exception($"Registration failed. Status: {status}");
                            }
                        }
                        else
                        {
                            //
                            // XML response handling
                            // 

                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(responseString);

                            var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                            if (status?.Trim() == "200")
                            {
                                MessageBox.Show("Service successfully registered.");
                            }
                            else
                            {
                                throw new Exception($"Registration failed. Status: {status}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\n" +
                        $"Details:\n{ex.StackTrace}");
                }

                SessionToken = null;
                _buttonRegister.Enabled = false;
            }
        }

        private void _buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private async void _buttonGetViewers_Click(object sender, EventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var baseUrl = _textBoxHubURL.Text + "Registry";

                    // Use UriBuilder for more complex query strings
                    var uriBuilder = new UriBuilder(baseUrl);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo SessionToken support
                    //query["FileFormat"] = ".glb";
                    query["Accept"] = "application/xml";
                    //query["Accept"] = "application/json";
                    uriBuilder.Query = query.ToString();

                    HttpResponseMessage response = await client.GetAsync(uriBuilder.ToString());

                    string responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseString);

                    _textBoxLog.Text = NormalizeLineEndings(responseString);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n" +
                    $"Details:\n{ex.StackTrace}");
            }
        }

        private async void _buttonGetConvertors_Click(object sender, EventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var baseUrl = _textBoxHubURL.Text + "Registry";

                    // Use UriBuilder for more complex query strings
                    var uriBuilder = new UriBuilder(baseUrl);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo SessionToken support
                    query["ServiceType"] = "Convertor";
                    query["OriginFormat"] = ".ifc";
                    //query["OriginFormat"] = ".step";
                    query["Accept"] = "application/xml";
                    //query["Accept"] = "application/json";
                    uriBuilder.Query = query.ToString();

                    HttpResponseMessage response = await client.GetAsync(uriBuilder.ToString());

                    string responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseString);

                    _textBoxLog.Text = NormalizeLineEndings(responseString);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n" +
                    $"Details:\n{ex.StackTrace}");
            }
        }

        private async void _buttonGetThumbnailGenerators_Click(object sender, EventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var baseUrl = _textBoxHubURL.Text + "Registry";

                    // Use UriBuilder for more complex query strings
                    var uriBuilder = new UriBuilder(baseUrl);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo SessionToken support
                    query["ServiceType"] = "ThumbnailGenerator";
                    //query["OriginFormat"] = ".ifc";
                    //query["OriginFormat"] = ".step";
                    query["Accept"] = "application/xml";
                    //query["Accept"] = "application/json";
                    uriBuilder.Query = query.ToString();

                    HttpResponseMessage response = await client.GetAsync(uriBuilder.ToString());

                    string responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseString);

                    _textBoxLog.Text = NormalizeLineEndings(responseString);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n" +
                    $"Details:\n{ex.StackTrace}");
            }
        }

        private async void _buttonGetMeshFilters_Click(object sender, EventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var baseUrl = _textBoxHubURL.Text + "Registry";

                    // Use UriBuilder for more complex query strings
                    var uriBuilder = new UriBuilder(baseUrl);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo SessionToken support
                    query["ServiceType"] = "MeshFilter";
                    query["Accept"] = "application/xml";
                    //query["Accept"] = "application/json";
                    uriBuilder.Query = query.ToString();

                    HttpResponseMessage response = await client.GetAsync(uriBuilder.ToString());

                    string responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseString);

                    _textBoxLog.Text = NormalizeLineEndings(responseString);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n" +
                    $"Details:\n{ex.StackTrace}");
            }
        }

        private async void _buttonGetPhotogrammetryServices_Click(object sender, EventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var baseUrl = _textBoxHubURL.Text + "Registry";

                    // Use UriBuilder for more complex query strings
                    var uriBuilder = new UriBuilder(baseUrl);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo SessionToken support
                    query["ServiceType"] = "Photogrammetry";
                    query["Accept"] = "application/xml";
                    //query["Accept"] = "application/json";
                    uriBuilder.Query = query.ToString();

                    HttpResponseMessage response = await client.GetAsync(uriBuilder.ToString());

                    string responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseString);

                    _textBoxLog.Text = NormalizeLineEndings(responseString);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n" +
                    $"Details:\n{ex.StackTrace}");
            }
        }

        private async void _buttonGetRepositories_Click(object sender, EventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var baseUrl = _textBoxHubURL.Text + "Registry";

                    // Use UriBuilder for more complex query strings
                    var uriBuilder = new UriBuilder(baseUrl);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo SessionToken support
                    query["ServiceType"] = "Repository";
                    query["Accept"] = "application/xml";
                    //query["Accept"] = "application/json";
                    uriBuilder.Query = query.ToString();

                    HttpResponseMessage response = await client.GetAsync(uriBuilder.ToString());

                    string responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseString);

                    _textBoxLog.Text = NormalizeLineEndings(responseString);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n" +
                    $"Details:\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Normalizes line endings in a string to CRLF for correct display in WinForms TextBox.
        /// </summary>
        private static string NormalizeLineEndings(string text)
        {
            // Normalize all variants (\r\n, \r, \n) to \r\n
            return text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "\r\n");
        }        

        private string? SessionToken { get; set; } = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740";
    }
}
