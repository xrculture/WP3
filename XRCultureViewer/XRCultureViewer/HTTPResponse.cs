namespace XRCultureViewer
{
    public class HTTPResponse
    {
        public static readonly string SuccessXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ViewModelResponse>
    <Status>200</Status>
</ViewModelResponse>";

        public static readonly string SuccessJSON =
@"{
    ""Status"": 200
}";

        public static readonly string SuccessWithParametersXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ViewModelResponse>
    <Status>200</Status>
    <Parameters>%PARAMETERS%</Parameters>
</ViewModelResponse>";

        public static readonly string SuccessWithParametersJSON =
@"{
    ""Status"": 200,
    ""Parameters"": %PARAMETERS%
}";

        public static readonly string BadRequestXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ViewModelResponse>
    <Status>400</Status>
    <Message>%MESSAGE%</Message>
</ViewModelResponse>";

        public static readonly string BadRequestJSON =
@"{
    ""Status"": 400,
    ""Message"": ""%MESSAGE%""
}";

        public static readonly string ServerErrorXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ViewModelResponse>
    <Status>500</Status>
    <Message>%MESSAGE%</Message>
</ViewModelResponse>";

        public static readonly string ServerErrorJSON =
@"{
    ""Status"": 500,
    ""Message"": ""%MESSAGE%""
}";

        public static readonly string NotFoundXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
    <ViewModelResponse>
    <Status>404</Status>
    <Message>%MESSAGE%</Message>
</ViewModelResponse>";

        public static readonly string NotFoundJSON =
@"{
    ""Status"": 404,
    ""Message"": ""%MESSAGE%""
}";

        public static readonly string ModelLoadingResponseSuccessXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
    <ModelLoadingResponse>
	<Status>200</Status>
	<SessionToken>%SESSION_TOKEN%</SessionToken>
	<Message>Success</Message>
	<LoadedContent>%SIZE%</LoadedContent>
	<Metadata></Metadata>
	<Endpoint>%ENDPOINT%</Endpoint>
    <Thumbnail>%THUMBNAIL%</Thumbnail>
</ModelLoadingResponse>";

        public static readonly string ModelLoadingResponseSuccessJSON =
@"{
    ""Status"": 200,
    ""SessionToken"": ""%SESSION_TOKEN%"",
    ""Message"": ""Success"",
    ""LoadedContent"": %SIZE%,
    ""Metadata"": {},
    ""Endpoint"": ""%ENDPOINT%"",
    ""Thumbnail"": ""%THUMBNAIL%""
}";

        public static readonly string ModelLoadingResponseBadRequestXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
    <ModelLoadingResponse>
	<Status>400</Status>
    <Message>%MESSAGE%</Message>
</ModelLoadingResponse>";

        public static readonly string ModelLoadingResponseBadRequestJSON =
@"{
    ""Status"": 400,
    ""Message"": ""%MESSAGE%""
}";
    }
}
