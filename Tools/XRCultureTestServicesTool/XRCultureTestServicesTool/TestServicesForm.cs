using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;

namespace XRCultureTestServicesTool
{
    public partial class TestServicesForm : Form
    {
        private SignalRLogClient _signalRClient;
        private string _3DReconstructionServerUrl;

        public TestServicesForm()
        {
            InitializeComponent();

            //_3DReconstructionServerUrl = "http://xrculture.rdf.bg:30026/";
            _3DReconstructionServerUrl = "http://localhost:5260/"; // Debug or Docker
            _signalRClient = new SignalRLogClient(_3DReconstructionServerUrl, _textBoxLog);
        }

        private async void TestServicesForm_Load(object sender, EventArgs e)
        {
            //_textBoxHubURL.Text = "http://xrculturehub.rdf.bg:30026/";
            _textBoxHubURL.Text = "http://localhost:5130/"; // Debug or Docker
        }

        private async void TestServicesForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            await _signalRClient.DisconnectAsync();
        }

        private void _buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private async void _buttonViewModel_Click(object sender, EventArgs e)
        {
            // 1. base64 request
            ViewModelRequest();

            // 2. Multi-part request
            //ViewModelMultiPartRequest();
        }

        private async void ViewModelRequest()
        {
            var openFileDialog = new OpenFileDialog()
            {
                FileName = "3D Model file",
                Filter = """
                    All Supported Files (*.binz;*.zae;*.objz;*.glb;*.gltf;*.splat;*.ply)|*.binz;*.zae;*.objz;*.glb;*.gltf;*.splat;*.ply|
                    BIN Compressed files (*.binz)|*.binz|
                    COLLADA Compressed files (*.zae)|*.zae|
                    OBJ Compressed files (*.objz)|*.objz|
                    glTF Binary files (*.glb)|*.glb|
                    glTF files (*.gltf)|*.gltf|
                    Gaussian Splatting files (*.splat;*.ply)|*.splat;*.ply"
                    """,
                Title = "Open 3D Model file"
            };

            //var viewerUrl = "http://localhost:6130/GaussianSplattingViewer"; // Debug or Docker
            //var viewerUrl = "http://xrcultureviewer.rdf.bg:30026/GaussianSplattingViewer";

            //var viewerUrl = "http://xrcultureviewer.rdf.bg:30026/Viewer";
            var viewerUrl = "http://localhost:6130/Viewer"; // Debug or Docker

            //var viewerUrl = "https://3dapi.repox.io/xrculture/models"; // REPOX for testing

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _textBoxLog.Text = "";

                    var fileInfo = new FileInfo(openFileDialog.FileName);
                    byte[] fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    string base64Content = Convert.ToBase64String(fileBytes);

                    //
                    // JSON request for Viewer REST Service
                    //

                    string modelLoadingRequestJSON = JsonConvert.SerializeObject(new
                    {
                        SessionToken = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740",//SessionToken ?? string.Empty, //todo: Session Token support
                        Source = new
                        {
                            LocalSource = new
                            {
                                FileName = fileInfo.Name,
                                FileExtension = Path.GetExtension(openFileDialog.FileName),
                                FileDimension = fileInfo.Length,
                                FileContent = base64Content
                            }
                        },
                        oEmbed = "False"
                    }, Newtonsoft.Json.Formatting.Indented);

                    //
                    // XML request for Viewer REST Service
                    //

                    //todo: Session Token support
                    string modelLoadingRequestXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ModelLoadingRequest>
    <SessionToken>e3be7cc2-3a7e-45e6-9a88-bd364e6de740</SessionToken>
    <Source>
        <LocalSource dimension=""%SIZE%"" extension=""%EXTENSION%"" filename=""%NAME%"">
            %BASE64_CONTENT%
        </LocalSource>
    </Source>
    <oEmbed>False</oEmbed>
</ModelLoadingRequest>";
                    modelLoadingRequestXML = modelLoadingRequestXML
                           .Replace("%NAME%", fileInfo.Name)
                           .Replace("%SIZE%", fileInfo.Length.ToString())
                           .Replace("%EXTENSION%", Path.GetExtension(openFileDialog.FileName))
                           .Replace("%BASE64_CONTENT%", base64Content);

                    using (var handler = new HttpClientHandler())
                    {
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                        // Allow cookies to be stored and sent with requests
                        handler.UseCookies = true;
                        handler.CookieContainer = new System.Net.CookieContainer();

                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromMinutes(10);

                            // JSON
                            var content = new StringContent(modelLoadingRequestJSON, Encoding.UTF8, "application/json");
                            _textBoxLog.Text += NormalizeLineEndings(modelLoadingRequestJSON.Substring(0, modelLoadingRequestJSON.Length > 1024 ? 1024 : modelLoadingRequestJSON.Length));
                            if (modelLoadingRequestJSON.Length > 1024)
                            {
                                _textBoxLog.Text += "...\r\n";
                            }

                            // XML
                            //var content = new StringContent(
                            //    JsonConvert.SerializeObject(modelLoadingRequestXML, Newtonsoft.Json.Formatting.Indented), 
                            //    Encoding.UTF8, "application/xml");
                            //_textBoxLog.Text += NormalizeLineEndings(modelLoadingRequestXML);

                            HttpResponseMessage response = await client.PostAsync(viewerUrl, content);
                            string responseString = await response.Content.ReadAsStringAsync();
                            Console.WriteLine(responseString);

                            _textBoxLog.Text += NormalizeLineEndings(responseString);

                            //
                            // JSON Response
                            //

                            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                            var status = jsonResponse?.Status;
                            if (status != "200")
                            {
                                throw new Exception($"Viewer REST Service returned error: {status}");
                            }

                            var modelUrl = jsonResponse?.Endpoint?.ToString();
                            if (string.IsNullOrEmpty(modelUrl))
                            {
                                throw new Exception("Viewer REST Service did not return a 'URL'.");
                            }

                            // Decode HTML entities in the URL if any
                            modelUrl = System.Net.WebUtility.HtmlDecode(modelUrl);

                            //
                            // XML Response
                            //

                            // Open the URL in the default browser
                            try
                            {
                                if (OperatingSystem.IsWindows())
                                {
                                    Process.Start(new ProcessStartInfo(modelUrl) { UseShellExecute = true });
                                }
                                else if (OperatingSystem.IsLinux())
                                {
                                    Process.Start("xdg-open", modelUrl);
                                }
                                else if (OperatingSystem.IsMacOS())
                                {
                                    Process.Start("open", modelUrl);
                                }
                                else
                                {
                                    MessageBox.Show($"URL is available but cannot be opened automatically on this platform: {modelUrl}");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error opening browser: {ex.Message}");
                            }

                            // Process the response
                            //if (responseString.Contains("<ModelLoadingResponse>"))
                            //{
                            //    var xmlDoc = new XmlDocument();
                            //    xmlDoc.LoadXml(responseString);

                            //    var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                            //    if (status?.Trim() != "200")
                            //    {
                            //        throw new Exception($"Viewer REST Service returned error: {status}");
                            //    }

                            //    var modelUrl = xmlDoc.SelectSingleNode("/ModelLoadingResponse/Endpoint")?.InnerText;
                            //    if (string.IsNullOrEmpty(modelUrl))
                            //    {
                            //        throw new Exception("Viewer REST Service did not return a 'URL'.");
                            //    }

                            //    // Open the URL in the default browser
                            //    try
                            //    {
                            //        if (OperatingSystem.IsWindows())
                            //        {
                            //            Process.Start(new ProcessStartInfo(modelUrl) { UseShellExecute = true });
                            //        }
                            //        else if (OperatingSystem.IsLinux())
                            //        {
                            //            Process.Start("xdg-open", modelUrl);
                            //        }
                            //        else if (OperatingSystem.IsMacOS())
                            //        {
                            //            Process.Start("open", modelUrl);
                            //        }
                            //        else
                            //        {
                            //            MessageBox.Show($"URL is available but cannot be opened automatically on this platform: {modelUrl}");
                            //        }
                            //    }
                            //    catch (Exception ex)
                            //    {
                            //        MessageBox.Show($"Error opening browser: {ex.Message}");
                            //    }
                            //}
                            //else
                            //{
                            //    MessageBox.Show($"Authentication failed. Server response: {responseString.Substring(0, Math.Min(responseString.Length, 500))}...");
                            //}
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\n" +
                        $"Details:\n{ex.StackTrace}");
                }
                finally
                {
                    SessionToken = null;
                }
            }
        }

        private async void ViewModelMultiPartRequest()
        {
            var openFileDialog = new OpenFileDialog()
            {
                FileName = "3D Model file",
                Filter = """
                    All Supported Files (*.binz;*.zae;*.objz;*.glb;*.gltf;*.splat;*.ply)|*.binz;*.zae;*.objz;*.glb;*.gltf;*.splat;*.ply|
                    BIN Compressed files (*.binz)|*.binz|
                    COLLADA Compressed files (*.zae)|*.zae|
                    OBJ Compressed files (*.objz)|*.objz|
                    glTF Binary files (*.glb)|*.glb|
                    glTF files (*.gltf)|*.gltf|
                    Gaussian Splatting files (*.splat;*.ply)|*.splat;*.ply"
                    """,
                Title = "Open 3D Model file"
            };

            string viewerBaseUrl = "http://localhost:5130/"; // Debug or Docker

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // xml request for Viewer REST Service
                    string viewModelRequest =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ViewModelRequest>
    <Name>%NAME%</Name>
    <Parameters></Parameters>
</ViewModelRequest>";
                    viewModelRequest = viewModelRequest.Replace("%NAME%", Path.GetFileName(openFileDialog.FileName));

                    using (var handler = new HttpClientHandler())
                    {
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                        // Allow cookies to be stored and sent with requests
                        handler.UseCookies = true;
                        handler.CookieContainer = new System.Net.CookieContainer();

                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromMinutes(10);

                            var viewerUrl = viewerBaseUrl + "Viewer";
                            using (var form = new MultipartFormDataContent())
                            {
                                // Add XML request as a form part
                                form.Add(new StringContent(viewModelRequest, Encoding.UTF8, "application/xml"), "request", "request.xml");

                                // Add the zip file as a form part
                                using (var fileStream = File.OpenRead(openFileDialog.FileName))
                                {
                                    if (fileStream == null || fileStream.Length == 0)
                                        throw new Exception("File stream is null or empty. Please check the file path.");

                                    var fileContent = new StreamContent(fileStream);
                                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                                    form.Add(fileContent, "file", Path.GetFileName(openFileDialog.FileName));

                                    var response = await client.PostAsync(viewerUrl, form);
                                    string responseString = await response.Content.ReadAsStringAsync();
                                    Console.WriteLine(responseString);

                                    _textBoxLog.Text = NormalizeLineEndings(responseString);

                                    // Process the response
                                    if (responseString.Contains("<ModelLoadingResponse>"))
                                    {
                                        var xmlDoc = new XmlDocument();
                                        xmlDoc.LoadXml(responseString);

                                        var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                                        if (status?.Trim() != "200")
                                        {
                                            throw new Exception($"Viewer REST Service returned error: {status}");
                                        }

                                        var modelUrl = xmlDoc.SelectSingleNode("//Parameters/URL")?.InnerText;
                                        if (string.IsNullOrEmpty(modelUrl))
                                        {
                                            throw new Exception("Viewer REST Service did not return a 'URL'.");
                                        }

                                        // Open the URL in the default browser
                                        try
                                        {
                                            if (OperatingSystem.IsWindows())
                                            {
                                                Process.Start(new ProcessStartInfo(modelUrl) { UseShellExecute = true });
                                            }
                                            else if (OperatingSystem.IsLinux())
                                            {
                                                Process.Start("xdg-open", modelUrl);
                                            }
                                            else if (OperatingSystem.IsMacOS())
                                            {
                                                Process.Start("open", modelUrl);
                                            }
                                            else
                                            {
                                                MessageBox.Show($"URL is available but cannot be opened automatically on this platform: {modelUrl}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show($"Error opening browser: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Authentication failed. Server response: {responseString.Substring(0, Math.Min(responseString.Length, 500))}...");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\n" +
                        $"Details:\n{ex.StackTrace}");
                }
                finally
                {
                    SessionToken = null;
                }
            }
        }

        private async void _buttonViewModelXML_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog()
            {
                FileName = "XML file",
                Filter = "XML files (*.xml)|*.xml",
                Title = "Open XML file"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var streamReader = new StreamReader(openFileDialog.FileName);
                    var viewRequest = streamReader.ReadToEnd();

                    using (HttpClient client = new HttpClient())
                    {
                        //var url = "http://xrcultureviewer.rdf.bg:30026/Viewer";
                        var url = "http://localhost:6130/Viewer"; // Debug or Docker
                        //var url = "https://3dapi.repox.io/xrculture/models"; // REPOX for testing
                        var content = new StringContent(
                            JsonConvert.SerializeObject(viewRequest, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8, "application/xml");

                        HttpResponseMessage response = await client.PostAsync(url, content);

                        string responseString = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseString);

                        _textBoxLog.Text = NormalizeLineEndings(responseString);

                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(responseString);

                        var doc = new XmlDocument();
                        doc.LoadXml(responseString);

                        // XmlDocument decodes HTML entities
                        var modelUrl = xmlDoc.SelectSingleNode("/ModelLoadingResponse/Endpoint")?.InnerText;
                        if (string.IsNullOrEmpty(modelUrl))
                        {
                            throw new Exception("Viewer REST Service did not return a 'URL'.");
                        }

                        // Open the URL in the default browser
                        try
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                Process.Start(new ProcessStartInfo(modelUrl) { UseShellExecute = true });
                            }
                            else if (OperatingSystem.IsLinux())
                            {
                                Process.Start("xdg-open", modelUrl);
                            }
                            else if (OperatingSystem.IsMacOS())
                            {
                                Process.Start("open", modelUrl);
                            }
                            else
                            {
                                MessageBox.Show($"URL is available but cannot be opened automatically on this platform: {modelUrl}");
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error opening browser: {ex.Message}");
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
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo: Session Token support
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
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo: Session Token support
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

        private void _button3DReconstructionOpenMVG_MVS_Click(object sender, EventArgs e)
        {
            Create3DModelRequest("openMVG-openMVS");
        }

        private void _button3DReconstructionNeRFStudio_Click(object sender, EventArgs e)
        {
            Create3DModelRequest("NeRFStudio");
        }

        private async void Create3DModelRequest(string workflow)
        {
            string model = string.Empty;
            string inputDir = string.Empty;

            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select input folder containing images for 3D reconstruction";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() != DialogResult.OK)
                {
                    return; // User cancelled
                }

                inputDir = folderDialog.SelectedPath;

                // Validate that the folder contains image files
                var imageFiles = Directory.EnumerateFiles(inputDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

                if (!imageFiles.Any())
                {
                    MessageBox.Show("Selected folder does not contain any image files (jpg, jpeg, png).");
                    return;
                }

                // Extract model name from folder name
                model = Path.GetFileName(inputDir);
            }

            var url = $"{_3DReconstructionServerUrl}TaskManager?handler=Create3DModel"; // Debug or Docker

            string create3DModelRequest =
                @"<Create3DModelRequest>
                    <Model>%MODEL%</Model>
                    <Workflow>%WORKFLOW%</Workflow>
                </Create3DModelRequest>";

            var tempFolder = string.Empty;

            try
            {
                await _signalRClient.InitializeSignalRLogHubConnectionAsync();
                await _signalRClient.InitializeSignalRStatusHubConnectionAsync();

                create3DModelRequest = create3DModelRequest.
                    Replace("%MODEL%", model).
                    Replace("%WORKFLOW%", workflow);

                tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                CreateZipArchive(inputDir, tempFolder, "model.zip");

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromMinutes(60);

                        using (var form = new MultipartFormDataContent())
                        {
                            form.Add(new StringContent(create3DModelRequest, Encoding.UTF8, "application/xml"), "request", "request.xml");

                            using (var fileStream = File.OpenRead(Path.Combine(tempFolder, "model.zip")))
                            {
                                if (fileStream == null || fileStream.Length == 0)
                                {
                                    throw new Exception("File stream is null or empty. Please check the file path.");
                                }

                                var fileContent = new StreamContent(fileStream);
                                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                                form.Add(fileContent, "file", "model.zip");

                                var response = await RetryPostAsync(client, url, form);
                                if (response == null)
                                {
                                    throw new Exception("POST request failed.");
                                }

                                string responseString = await response.Content.ReadAsStringAsync();
                                Console.WriteLine(responseString);

                                _textBoxLog.Text = NormalizeLineEndings(responseString);

                                var xmlDoc = new XmlDocument();
                                xmlDoc.LoadXml(responseString);

                                var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                                if (status?.Trim() != "200")
                                {
                                    throw new Exception($"Server returned error: {status}");
                                }

                                var taskId = xmlDoc.SelectSingleNode("//Parameters/TaskId")?.InnerText;
                                if (string.IsNullOrEmpty(taskId))
                                {
                                    throw new Exception("Server did not return a 'TaskId'.");
                                }

                                // GET TaskStatus
                                var taskStatusUrl = $"{_3DReconstructionServerUrl}TaskManager?handler=TaskStatus&taskId={taskId}";
                                response = await client.GetAsync(taskStatusUrl.ToString());
                                responseString = await response.Content.ReadAsStringAsync();
                                Console.WriteLine(responseString);

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\nDetails:\n{ex.StackTrace}");
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(tempFolder) && Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\nDetails:\n{ex.StackTrace}");
                }
            }
        }

        public void CreateZipArchive(string inputDir, string outputDir, string zipName)
        {
            Directory.CreateDirectory(outputDir);

            string archivePath = Path.Combine(outputDir, zipName);

            using (var zipStream = new FileStream(archivePath, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    // Add .bin and .jpg files
                    var files = Directory.EnumerateFiles(inputDir, "*.*", SearchOption.AllDirectories).Where(f =>
                        f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));

                    foreach (var file in files)
                    {
                        string entryName = Path.GetRelativePath(inputDir, file);
                        archive.CreateEntryFromFile(file, entryName);
                    }
                }
            }
        }

        public async Task<HttpResponseMessage?> RetryPostAsync(HttpClient client, string url, HttpContent content, int maxRetries = 3)
        {
            int retryCount = 0;
            HttpResponseMessage? response = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    response = await client.PostAsync(url, content);
                    return response;
                }
                catch (HttpRequestException)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;

                    int delayMs = (int)Math.Pow(2, retryCount) * 1000;
                    await Task.Delay(delayMs);
                }
            }

            return response;
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
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo: Session Token support
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

        private async void _buttonConvertModel_Click(object sender, EventArgs e)
        {
            await ConvertModelRequest();
        }

        private async Task ConvertModelRequest()
        {
            var openFileDialog = new OpenFileDialog()
            {
                FileName = "3D Model file",
                Filter = "All Supported Files (*.ifc;*.step;*.stp)|*.ifc;*.step;*.stp|IFC files (*.ifc)|*.ifc|STEP files (*.step;*.stp)|*.step;*.stp",
                Title = "Open 3D Model file"
            };

            //var servicesUrl = "http://xrcultureservices.rdf.bg:30026/Convert";
            var servicesUrl = "http://localhost:6140/Convert"; // Debug or Docker

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _textBoxLog.Text = "";

                    var fileInfo = new FileInfo(openFileDialog.FileName);
                    byte[] fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    string base64Content = Convert.ToBase64String(fileBytes);

                    //
                    // JSON request for Viewer REST Service
                    //

                    string modelConversionRequestJSON = JsonConvert.SerializeObject(new
                    {
                        SessionToken = "cvt-8b1d5d1d-4d11-4f34-9b69-0a3d91f1b7f1",//SessionToken ?? string.Empty, //todo: Session Token support
                        OriginFormat = new
                        {
                            extension = ".ifc",
                            mimetype = "application/x-extension-ifc"
                        },
                        DestinationFormat = new
                        {
                            extension = ".glb",
                            mimetype = "model/gltf-binary"
                        },
                        Source = new
                        {
                            LocalSource = new
                            {
                                FileName = fileInfo.Name,
                                FileExtension = Path.GetExtension(openFileDialog.FileName),
                                FileDimension = fileInfo.Length,
                                FileContent = base64Content
                            }
                        }
                    }, Newtonsoft.Json.Formatting.Indented);

                    //
                    // XML request for Viewer REST Service
                    //

                    var originFormatExtension = Path.GetExtension(openFileDialog.FileName).ToLower();

                    //todo: Session Token support
                    string modelConversionRequestXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ConversionRequest>
    <SessionToken>cvt-8b1d5d1d-4d11-4f34-9b69-0a3d91f1b7f1</SessionToken>
    <OriginFormat extension=""%ORIGINFORMAT%"" mimetype=""application/x-extension-ifc"" />
    <DestinationFormat extension="".glb"" mimetype=""model/gltf-binary"" />
    <Source>
        <LocalSource dimension=""%SIZE%"" extension=""%EXTENSION%"" filename=""%NAME%"">
            %BASE64_CONTENT%
        </LocalSource>
    </Source>
</ConversionRequest>";
                    modelConversionRequestXML = modelConversionRequestXML
                        .Replace("%ORIGINFORMAT%", originFormatExtension)
                        .Replace("%NAME%", fileInfo.Name)
                        .Replace("%SIZE%", fileInfo.Length.ToString())
                        .Replace("%EXTENSION%", originFormatExtension)
                        .Replace("%BASE64_CONTENT%", base64Content);

                    using (var handler = new HttpClientHandler())
                    {
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                        // Allow cookies to be stored and sent with requests
                        handler.UseCookies = true;
                        handler.CookieContainer = new System.Net.CookieContainer();

                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromMinutes(10);

                            // JSON
                            //var content = new StringContent(modelConversionRequestJSON, Encoding.UTF8, "application/json");
                            //_textBoxLog.Text += NormalizeLineEndings(modelConversionRequestJSON.Substring(0, modelConversionRequestJSON.Length > 512 ? 512 : modelConversionRequestJSON.Length));
                            //if (modelConversionRequestJSON.Length > 512)
                            //{
                            //    _textBoxLog.Text += "...\r\n";
                            //}

                            // XML
                            var content = new StringContent(
                                JsonConvert.SerializeObject(modelConversionRequestXML, Newtonsoft.Json.Formatting.Indented),
                                Encoding.UTF8, "application/xml");
                            _textBoxLog.Text += NormalizeLineEndings(modelConversionRequestXML.Substring(0, modelConversionRequestXML.Length > 1024 ? 1024 : modelConversionRequestXML.Length));
                            if (modelConversionRequestXML.Length > 1024)
                            {
                                _textBoxLog.Text += "...\r\n";
                            }

                            HttpResponseMessage response = await client.PostAsync(servicesUrl, content);
                            string responseString = await response.Content.ReadAsStringAsync();

                            _textBoxLog.Text += NormalizeLineEndings(responseString.Substring(0, responseString.Length > 1024 ? 1024 : responseString.Length));
                            if (responseString.Length > 1024)
                            {
                                _textBoxLog.Text += "...\r\n";
                            }

                            //
                            // JSON Response
                            //

                            //dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                            //var status = jsonResponse?.Status;
                            //if (status != "200")
                            //{
                            //    throw new Exception($"Viewer REST Service returned error: {status}");
                            //}

                            //base64Content = jsonResponse?.ConvertedFile?.base64Content?.ToString();
                            //if (string.IsNullOrEmpty(base64Content))
                            //{
                            //    throw new Exception("Viewer REST Service did not return a 'ConvertedFile'.");
                            //}

                            //SaveFileDialog saveFileDialog = new SaveFileDialog()
                            //{
                            //    FileName = Path.GetFileNameWithoutExtension(openFileDialog.FileName) + ".glb",
                            //    Filter = "glTF Binary files (*.glb)|*.glb",
                            //    Title = "Save converted 3D Model file"
                            //};

                            //if (saveFileDialog.ShowDialog() == DialogResult.OK)
                            //{
                            //    var filePath = saveFileDialog.FileName;
                            //    fileBytes = Convert.FromBase64String(base64Content);
                            //    File.WriteAllBytes(filePath, fileBytes);
                            //}

                            //
                            // XML Response
                            //

                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(responseString);

                            var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                            if (status?.Trim() != "200")
                            {
                                throw new Exception($"Viewer REST Service returned error: {status}");
                            }

                            base64Content = xmlDoc?.SelectSingleNode("//ConvertedFile/LocalResult")?.InnerText;
                            if (string.IsNullOrEmpty(base64Content))
                            {
                                throw new Exception("Viewer REST Service did not return a 'ConvertedFile'.");
                            }

                            SaveFileDialog saveFileDialog = new SaveFileDialog()
                            {
                                FileName = Path.GetFileNameWithoutExtension(openFileDialog.FileName) + ".glb",
                                Filter = "glTF Binary files (*.glb)|*.glb",
                                Title = "Save converted 3D Model file"
                            };

                            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                            {
                                var filePath = saveFileDialog.FileName;
                                fileBytes = Convert.FromBase64String(base64Content);
                                File.WriteAllBytes(filePath, fileBytes);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\n" +
                        $"Details:\n{ex.StackTrace}");
                }
                finally
                {
                    SessionToken = null;
                }
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
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo: Session Token support
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

        private async void _buttonMeshLabDecimate_Click(object sender, EventArgs e)
        {
            string inputDir = string.Empty;
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select input folder containing Wavefront OBJ model and textures";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() != DialogResult.OK)
                {
                    return; // User cancelled
                }

                inputDir = folderDialog.SelectedPath;
            }

            var tempDir = string.Empty;

            try
            {
                tempDir = await ApplyMeshLabFilter("meshing_decimation_quadric_edge_collapse_with_texture", inputDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\nDetails:\n{ex.StackTrace}");
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\nDetails:\n{ex.StackTrace}");
                }
            }
        }

        private async void _buttonMeshLabSubdivide_Click(object sender, EventArgs e)
        {
            string inputDir = string.Empty;
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select input folder containing Wavefront OBJ model and textures";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() != DialogResult.OK)
                {
                    return; // User cancelled
                }

                inputDir = folderDialog.SelectedPath;
            }

            var tempDir = string.Empty;

            try
            {
                tempDir = await ApplyMeshLabFilter("subdivision_ls3_loop", inputDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\nDetails:\n{ex.StackTrace}");
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\nDetails:\n{ex.StackTrace}");
                }
            }
        }

        private async Task<string> ApplyMeshLabFilter(string filterName, string inputDir)
        {
            var objFiles = Directory.EnumerateFiles(inputDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));

            if (objFiles.Count() != 1)
            {
                MessageBox.Show("Selected folder does not contain exactly one OBJ file.");
                return "";
            }            

            //var meshLabUrl = "http://xrculturemeshlab.rdf.bg:30026/";
            var meshLabUrl = "http://localhost:5195/"; // Debug or Docker

            // XML request for MeshLab Server
            string applyFilterRequest = 
@"<ApplyFilterRequest>
    <Name>%FILTER%</Name>
    <Parameters>
        <InputMesh>%INPUT_MESH%</InputMesh>
        <OutputMesh>%OUTPUT_MESH%</OutputMesh>
    </Parameters>
</ApplyFilterRequest>";
            applyFilterRequest = applyFilterRequest.Replace("%FILTER%", filterName);
            applyFilterRequest = applyFilterRequest.Replace("%INPUT_MESH%", Path.GetFileName(objFiles.First()));
            applyFilterRequest = applyFilterRequest.Replace("%OUTPUT_MESH%", Path.GetFileName(objFiles.First()));

            _textBoxLog.Text = NormalizeLineEndings(applyFilterRequest);

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            CreateZipArchive(inputDir, tempDir, "model.zip");

            using (var handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromMinutes(60);

                    using (var form = new MultipartFormDataContent())
                    {
                        // Add XML request as a form part
                        form.Add(new StringContent(applyFilterRequest, Encoding.UTF8, "application/xml"), "request", "request.xml");

                        // Add the zip file as a form part
                        using (var fileStream = File.OpenRead(Path.Combine(tempDir, "model.zip")))
                        {
                            if (fileStream == null || fileStream.Length == 0)
                            {
                                throw new Exception("File stream is null or empty. Please check the file path.");
                            }

                            var fileContent = new StreamContent(fileStream);
                            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                            form.Add(fileContent, "file", "model.zip");

                            var url = meshLabUrl + "Filters?handler=Apply2";
                            var response = await RetryPostAsync(client, url, form);
                            if (response == null)
                            {
                                throw new Exception("POST request failed.");
                            }

                            string responseString = await response.Content.ReadAsStringAsync();

                            _textBoxLog.Text += "\r\n\r\n";
                            _textBoxLog.Text += NormalizeLineEndings(responseString);

                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(responseString);

                            var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                            if (status?.Trim() != "200")
                            {
                                throw new Exception($"MeshLab Server returned error: {status}");
                            }

                            var resultId = xmlDoc.SelectSingleNode("//Parameters/ResultId")?.InnerText;
                            if (string.IsNullOrEmpty(resultId))
                            {
                                throw new Exception("MeshLab Server did not return a 'ResultId'.");
                            }

                            url = meshLabUrl + $"Filters?handler=ResultContents&resultId={Uri.EscapeDataString(resultId)}";
                            var resultResponse = await client.GetAsync(url);
                            resultResponse.EnsureSuccessStatusCode();
                            var dirJson = await resultResponse.Content.ReadAsStringAsync();

                            var files = System.Text.Json.JsonSerializer.Deserialize<List<string>>(dirJson);
                            if (files == null || files.Count == 0)
                            {
                                throw new Exception("MeshLab Server didn't return any result files.");
                            }

                            // Get the result files
                            string outputDir = Path.Combine(Path.GetDirectoryName(inputDir) ?? string.Empty, Path.GetFileName(inputDir) + $"_{filterName}");
                            Directory.CreateDirectory(outputDir);
                            foreach (var file in files)
                            {
                                // Retrieve
                                var fileUrl = meshLabUrl + $"Filters?handler=ResultFile&resultId={Uri.EscapeDataString(resultId)}&file={Uri.EscapeDataString(file)}";
                                var fileResponse = await client.GetAsync(fileUrl);
                                fileResponse.EnsureSuccessStatusCode();

                                // Save
                                var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(Path.Combine(outputDir, file), fileBytes);
                            }

                            url = meshLabUrl + $"Filters?handler=Result&resultId={Uri.EscapeDataString(resultId)}";
                            var deleteResponse = await client.DeleteAsync(url);
                            deleteResponse.EnsureSuccessStatusCode();

                            MessageBox.Show($"MeshLab filter applied successfully. Model saved to:\n{outputDir}");
                        }
                    }
                }

                return tempDir;
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
                    query["SessionToken"] = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740"; //#todo: Session Token support
                    query["ServiceType"] = "ThumbnailGenerator";
                    //query["FileFormat"] = ".ifc";
                    //query["FileFormat"] = ".step";
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

        private async void _buttonGenerateThumbnail_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog()
            {
                FileName = "3D Model file",
                Filter = """
                    All Supported Files (*.ifc;*.binz;*.zae;*.objz;*.glb;*.gltf;*.splat;*.ply)|*.ifc;*.binz;*.zae;*.objz;*.glb;*.gltf;*.splat;*.ply|
                    BIN Compressed files (*.binz)|*.binz|
                    COLLADA Compressed files (*.zae)|*.zae|
                    OBJ Compressed files (*.objz)|*.objz|
                    glTF Binary files (*.glb)|*.glb|
                    glTF files (*.gltf)|*.gltf|
                    Gaussian Splatting files (*.splat;*.ply)|*.splat;*.ply"
                    """,
                Title = "Open 3D Model file"
            };

            //var thumbnailGeneratorUrl = "http://xrcultureviewer.rdf.bg:30026/ThumbnailGenerator";
            var thumbnailGeneratorUrl = "http://localhost:6130/ThumbnailGenerator"; // Debug or Docker

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _textBoxLog.Text = "";

                    var fileInfo = new FileInfo(openFileDialog.FileName);
                    byte[] fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    string base64Content = Convert.ToBase64String(fileBytes);

                    //
                    // JSON request for Viewer REST Service
                    //

                    string modelLoadingRequestJSON = JsonConvert.SerializeObject(new
                    {
                        SessionToken = "e3be7cc2-3a7e-45e6-9a88-bd364e6de740",//SessionToken ?? string.Empty, //todo: Session Token support
                        Source = new
                        {
                            LocalSource = new
                            {
                                FileName = fileInfo.Name,
                                FileExtension = Path.GetExtension(openFileDialog.FileName),
                                FileDimension = fileInfo.Length,
                                FileContent = base64Content
                            }
                        }
                    }, Newtonsoft.Json.Formatting.Indented);

                    //
                    // XML request for Viewer REST Service
                    //

                    //todo: Session Token support
                    string modelLoadingRequestXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ModelLoadingRequest>
    <SessionToken>e3be7cc2-3a7e-45e6-9a88-bd364e6de740</SessionToken>
    <Source>
        <LocalSource dimension=""%SIZE%"" extension=""%EXTENSION%"" filename=""%NAME%"">
            %BASE64_CONTENT%
        </LocalSource>
    </Source>
</ModelLoadingRequest>";
                    modelLoadingRequestXML = modelLoadingRequestXML
                           .Replace("%NAME%", fileInfo.Name)
                           .Replace("%SIZE%", fileInfo.Length.ToString())
                           .Replace("%EXTENSION%", Path.GetExtension(openFileDialog.FileName))
                           .Replace("%BASE64_CONTENT%", base64Content);

                    using (var handler = new HttpClientHandler())
                    {
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                        // Allow cookies to be stored and sent with requests
                        handler.UseCookies = true;
                        handler.CookieContainer = new System.Net.CookieContainer();

                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromMinutes(10);

                            // JSON
                            var content = new StringContent(modelLoadingRequestJSON, Encoding.UTF8, "application/json");
                            _textBoxLog.Text += NormalizeLineEndings(modelLoadingRequestJSON.Substring(0, modelLoadingRequestJSON.Length > 1024 ? 1024 : modelLoadingRequestJSON.Length));
                            if (modelLoadingRequestJSON.Length > 1024)
                            {
                                _textBoxLog.Text += "...\r\n";
                            }

                            // XML
                            //var content = new StringContent(
                            //    JsonConvert.SerializeObject(modelLoadingRequestXML, Newtonsoft.Json.Formatting.Indented), 
                            //    Encoding.UTF8, "application/xml");
                            //_textBoxLog.Text += NormalizeLineEndings(modelLoadingRequestXML);

                            HttpResponseMessage response = await client.PostAsync(thumbnailGeneratorUrl, content);
                            string responseString = await response.Content.ReadAsStringAsync();
                            Console.WriteLine(responseString);

                            _textBoxLog.Text += NormalizeLineEndings(responseString);

                            //
                            // JSON Response
                            //

                            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                            var status = jsonResponse?.Status;
                            if (status != "200")
                            {
                                throw new Exception($"Viewer REST Service returned error: {status}");
                            }

                            var thumbnailUrl = jsonResponse?.Thumbnail?.ToString();
                            if (string.IsNullOrEmpty(thumbnailUrl))
                            {
                                throw new Exception("Viewer REST Service did not return a 'Thumbnail URL'.");
                            }

                            // Decode HTML entities in the URL if any
                            thumbnailUrl = System.Net.WebUtility.HtmlDecode(thumbnailUrl);

                            //
                            // XML Response
                            //

                            // Open the URL in the default browser
                            try
                            {
                                if (OperatingSystem.IsWindows())
                                {
                                    Process.Start(new ProcessStartInfo(thumbnailUrl) { UseShellExecute = true });
                                }
                                else if (OperatingSystem.IsLinux())
                                {
                                    Process.Start("xdg-open", thumbnailUrl);
                                }
                                else if (OperatingSystem.IsMacOS())
                                {
                                    Process.Start("open", thumbnailUrl);
                                }
                                else
                                {
                                    MessageBox.Show($"URL is available but cannot be opened automatically on this platform: {thumbnailUrl}");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error opening browser: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error:\n{ex.Message}\n" +
                        $"Details:\n{ex.StackTrace}");
                }
                finally
                {
                    SessionToken = null;
                }
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

        private string? SessionToken { get; set; }
    }
}
