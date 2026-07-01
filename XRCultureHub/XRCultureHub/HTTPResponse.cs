namespace XRCultureHub
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

        public static readonly string UnauthorizedXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ViewModelResponse>
    <Status>401</Status>
    <Message>%MESSAGE%</Message>
</ViewModelResponse>";

        public static readonly string UnauthorizedJSON =
@"{
    ""Status"": 401,
    ""Message"": ""%MESSAGE%""
}";

        public static readonly string ServerErrorXML =
@" <?xml version=""1.0"" encoding=""UTF-8""?>
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
    }
}
