namespace XRCultureServices
{
    public class HTTPResponse
    {
        public static readonly string SuccessXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ConversionResponse>
    <Status>200</Status>
</ConversionResponse>";

        public static readonly string SuccessJSON =
@"{
    ""Status"": 200
}";

        public static readonly string SuccessWithParametersXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ConversionResponse>
    <Status>200</Status>
    <Parameters>%PARAMETERS%</Parameters>
</ConversionResponse>";

        public static readonly string SuccessWithParametersJSON =
@"{
    ""Status"": 200,
    ""Parameters"": %PARAMETERS%
}";

        public static readonly string BadRequestXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ConversionResponse>
    <Status>400</Status>
    <Message>%MESSAGE%</Message>
</ConversionResponse>";

        public static readonly string BadRequestJSON =
@"{
    ""Status"": 400,
    ""Message"": ""%MESSAGE%""
}";

        public static readonly string ServerErrorXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ConversionResponse>
    <Status>500</Status>
    <Message>%MESSAGE%</Message>
</ConversionResponse>";

        public static readonly string ServerErrorJSON =
@"{
    ""Status"": 500,
    ""Message"": ""%MESSAGE%""
}";

        public static readonly string NotFoundJSON =
@"{
    ""Status"": 404,
    ""Message"": ""%MESSAGE%""
}";

        public static readonly string ConversionResponseSuccessXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ConversionResponse>
	<Status>200</Status>
	<SessionToken>%SESSION_TOKEN%</SessionToken>
	<Message>Success</Message>
    <ConvertedFile>
        <LocalResult dimension=""%SIZE%"" extension=""%EXTENSION%"" filename=""%FILENAME%"">
            %BASE64CONTENT%
        </LocalResult>
    </ConvertedFile>
</ConversionResponse>";

        public static readonly string ConversionResponseSuccessJSON =
@"{
    ""Status"": 200,
    ""SessionToken"": ""%SESSION_TOKEN%"",
    ""Message"": ""Success"",    
    ""ConvertedFile"": {
        ""dimension"": %SIZE%,
        ""extension"": ""%EXTENSION%"",
        ""filename"": ""%FILENAME%"",
        ""base64Content"": ""%BASE64CONTENT%""
    }
}";

        public static readonly string ConversionResponseBadRequestXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ConversionResponse>
	<Status>400</Status>
    <Message>%MESSAGE%</Message>
</ConversionResponse>";

        public static readonly string ConversionResponseBadRequestJSON =
@"{
    ""Status"": 400,
    ""Message"": ""%MESSAGE%""
}";
    }
}
