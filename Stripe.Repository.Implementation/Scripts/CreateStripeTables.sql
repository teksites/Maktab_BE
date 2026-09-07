CREATE TABLE IF NOT EXISTS stripe_payment_intent
(
    StripePaymentIntentKey BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    StudentCourseTransactionId BINARY(16) NULL,
    FamilyId BINARY(16) NULL,
    StripePaymentIntentId VARCHAR(255) NULL,
    StripeCustomerId VARCHAR(255) NULL,
    StripePaymentMethodId VARCHAR(255) NULL,
    StripeChargeId VARCHAR(255) NULL,
    IdempotencyKey VARCHAR(255) NOT NULL,
    ReferenceData VARCHAR(500) NULL,
    AmountMinor BIGINT UNSIGNED NOT NULL,
    AmountReceivedMinor BIGINT UNSIGNED NOT NULL DEFAULT 0,
    Currency CHAR(3) NOT NULL,
    StripeStatus VARCHAR(64) NOT NULL,
    IsLiveMode TINYINT(1) NOT NULL DEFAULT 0,
    LastPaymentErrorCode VARCHAR(128) NULL,
    LastPaymentErrorMessage VARCHAR(1024) NULL,
    CreatedAt DATETIME(6) NOT NULL,
    UpdatedOn DATETIME(6) NOT NULL,
    PRIMARY KEY (StripePaymentIntentKey),
    UNIQUE KEY UQ_StripePaymentIntent_IdempotencyKey (IdempotencyKey),
    UNIQUE KEY UQ_StripePaymentIntent_StripePaymentIntentId (StripePaymentIntentId),
    KEY IX_StripePaymentIntent_StudentCourseTransactionId (StudentCourseTransactionId),
    KEY IX_StripePaymentIntent_StripeChargeId (StripeChargeId)
);

CREATE TABLE IF NOT EXISTS stripe_refund
(
    StripeRefundKey BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    StudentCourseTransactionId BINARY(16) NULL,
    CoursePaymentId BINARY(16) NULL,
    StripePaymentIntentId VARCHAR(255) NULL,
    StripeChargeId VARCHAR(255) NULL,
    StripeRefundId VARCHAR(255) NULL,
    IdempotencyKey VARCHAR(255) NOT NULL,
    ReferenceData VARCHAR(500) NULL,
    AmountMinor BIGINT UNSIGNED NOT NULL,
    Currency CHAR(3) NOT NULL,
    StripeStatus VARCHAR(64) NOT NULL,
    IsLiveMode TINYINT(1) NOT NULL DEFAULT 0,
    FailureReason VARCHAR(1024) NULL,
    CreatedAt DATETIME(6) NOT NULL,
    UpdatedOn DATETIME(6) NOT NULL,
    PRIMARY KEY (StripeRefundKey),
    UNIQUE KEY UQ_StripeRefund_IdempotencyKey (IdempotencyKey),
    UNIQUE KEY UQ_StripeRefund_StripeRefundId (StripeRefundId),
    KEY IX_StripeRefund_StudentCourseTransactionId (StudentCourseTransactionId)
);

CREATE TABLE IF NOT EXISTS stripe_webhook_event
(
    StripeWebhookEventKey BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    StripeEventId VARCHAR(255) NOT NULL,
    EventType VARCHAR(128) NOT NULL,
    StripePaymentIntentId VARCHAR(255) NULL,
    StripeChargeId VARCHAR(255) NULL,
    StripeRefundId VARCHAR(255) NULL,
    IsLiveMode TINYINT(1) NOT NULL DEFAULT 0,
    SignatureHeader VARCHAR(2048) NOT NULL,
    RawPayload LONGTEXT NOT NULL,
    ProcessingStatus TINYINT UNSIGNED NOT NULL,
    AttemptCount INT UNSIGNED NOT NULL DEFAULT 1,
    ProcessingStartedAt DATETIME(6) NULL,
    ProcessedAt DATETIME(6) NULL,
    LastError TEXT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    UpdatedOn DATETIME(6) NOT NULL,
    PRIMARY KEY (StripeWebhookEventKey),
    UNIQUE KEY UQ_StripeWebhookEvent_StripeEventId (StripeEventId),
    KEY IX_StripeWebhookEvent_ProcessingStatus (ProcessingStatus)
);
