namespace SubTracker.Domain.Enums;

/// <summary>Статус підписки.</summary>
public enum SubscriptionStatus
{
    /// <summary>Активна — кошти списуються за графіком.</summary>
    Active = 1,

    /// <summary>Призупинена — списання тимчасово не відбуваються.</summary>
    Paused = 2,

    /// <summary>Скасована — більше не продовжується.</summary>
    Cancelled = 3
}

/// <summary>Періодичність списання.</summary>
public enum BillingPeriod
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4
}

/// <summary>Результат конкретного списання.</summary>
public enum PaymentStatus
{
    Succeeded = 1,
    Failed = 2,
    Refunded = 3
}

/// <summary>Тип способу оплати.</summary>
public enum PaymentMethodType
{
    Card = 1,
    PayPal = 2,
    BankTransfer = 3,
    GooglePay = 4,
    ApplePay = 5
}
