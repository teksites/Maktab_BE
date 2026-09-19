CREATE TABLE IF NOT EXISTS user_payment_gateway (
    UserPaymentGatewayId BINARY(16) NOT NULL,
    UserId BINARY(16) NOT NULL,
    PaymentGatewayType TINYINT UNSIGNED NOT NULL,
    UserPaymentGatewayCode VARCHAR(128) NOT NULL,
    ExternalCustomerId VARCHAR(128) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedOn DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (UserPaymentGatewayId),
    UNIQUE KEY UX_user_payment_gateway_user_provider (UserId, PaymentGatewayType),
    UNIQUE KEY UX_user_payment_gateway_provider_code (PaymentGatewayType, UserPaymentGatewayCode),
    KEY IX_user_payment_gateway_user_active (UserId, IsActive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- PaymentGatewayType: 1 = Helcim, 2 = Zeffy, 3 = Stripe, 255 = Other.
