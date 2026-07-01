namespace XRCulture3DReconstruction
{
    public class ViewModelHTTPResponse
    {
        public static readonly string Success =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ViewModelResponse><Status>200</Status></ViewModelResponse>";
        public static readonly string SuccessWithParameters =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ViewModelResponse><Status>200</Status><Parameters>%PARAMETERS%</Parameters></ViewModelResponse>";
        public static readonly string BadRequest =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ViewModelResponse><Status>400</Status><Message>%MESSAGE%</Message></ViewModelResponse>";
        public static readonly string ServerError =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ViewModelResponse><Status>500</Status><Message>%MESSAGE%</Message></ViewModelResponse>";
        public static readonly string NotFound =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ViewModelResponse><Status>404</Status><Message>%MESSAGE%</Message></ViewModelResponse>";
    }

    public class Create3DModelHTTPResponse
    {
        public static readonly string Success =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Create3DModelResponse><Status>200</Status></Create3DModelResponse>";
        public static readonly string SuccessWithParameters =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Create3DModelResponse><Status>200</Status><Parameters>%PARAMETERS%</Parameters></Create3DModelResponse>";
        public static readonly string BadRequest =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Create3DModelResponse><Status>400</Status><Message>%MESSAGE%</Message></Create3DModelResponse>";
        public static readonly string ServerError =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Create3DModelResponse><Status>500</Status><Message>%MESSAGE%</Message></Create3DModelResponse>";
        public static readonly string NotFound =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Create3DModelResponse><Status>404</Status><Message>%MESSAGE%</Message></Create3DModelResponse>";
        public static readonly string ServerBusy = 
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Create3DModelResponse><Status>503</Status><Message>Server is currently busy processing other requests. Please try again later.</Message></Create3DModelResponse>";
    }

    public class GetTaskStatusHTTPResponse
    {
        public static readonly string Success =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><GetTaskStatusResponse><Status>200</Status></GetTaskStatusResponse>";
        public static readonly string SuccessWithParameters =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><GetTaskStatusResponse><Status>200</Status><Parameters>%PARAMETERS%</Parameters></GetTaskStatusResponse>";
        public static readonly string BadRequest =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><GetTaskStatusResponse><Status>400</Status><Message>%MESSAGE%</Message></GetTaskStatusResponse>";
        public static readonly string ServerError =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><GetTaskStatusResponse><Status>500</Status><Message>%MESSAGE%</Message></GetTaskStatusResponse>";
        public static readonly string NotFound =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><GetTaskStatusResponse><Status>404</Status><Message>%MESSAGE%</Message></GetTaskStatusResponse>";
    }
}
