namespace SubTracker.Domain;

/// <summary>
/// Порушення правила предметної області (напр. «прострочена картка», «сума часток ≠ 100 %»).
/// Повідомлення адресоване користувачу і виводиться без технічних деталей.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string message, Exception innerException) : base(message, innerException) { }
}
