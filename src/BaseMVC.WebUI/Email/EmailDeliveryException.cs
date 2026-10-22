namespace BaseMVC.WebUI.Email;

public sealed class EmailDeliveryException(string message, Exception innerException)
    : Exception(message, innerException);
