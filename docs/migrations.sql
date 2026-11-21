IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentMethods] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Type] varchar(20) NOT NULL,
        [Title] nvarchar(100) NOT NULL,
        [Last4] char(4) NULL,
        [ExpiryDate] date NULL,
        [LastUsedAt] datetime2(0) NULL,
        CONSTRAINT [PK_PaymentMethods] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentMethods_Last4] CHECK ([Last4] IS NULL OR (LEN([Last4]) = 4 AND [Last4] NOT LIKE '%[^0-9]%')),
        CONSTRAINT [FK_PaymentMethods_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE TABLE [Subscriptions] (
        [Id] int NOT NULL IDENTITY,
        [OwnerId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [PaymentMethodId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] char(3) NOT NULL,
        [Period] varchar(20) NOT NULL,
        [NextChargeDate] date NOT NULL,
        [EndDate] date NULL,
        [Status] varchar(20) NOT NULL,
        [RemindDaysBefore] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Subscriptions_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Subscriptions_Currency] CHECK (LEN([Currency]) = 3),
        CONSTRAINT [CK_Subscriptions_RemindDaysBefore] CHECK ([RemindDaysBefore] BETWEEN 0 AND 30),
        CONSTRAINT [FK_Subscriptions_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Subscriptions_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Subscriptions_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [SubscriptionId] int NOT NULL,
        [PaymentMethodId] int NOT NULL,
        [ChargedAt] datetime2(0) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] char(3) NOT NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_Payments_PaymentMethods_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [PaymentMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Subscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [Subscriptions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE TABLE [SubscriptionMembers] (
        [SubscriptionId] int NOT NULL,
        [UserId] int NOT NULL,
        [SharePercent] decimal(5,2) NOT NULL,
        [JoinedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_SubscriptionMembers] PRIMARY KEY ([SubscriptionId], [UserId]),
        CONSTRAINT [CK_SubscriptionMembers_SharePercent] CHECK ([SharePercent] > 0 AND [SharePercent] <= 100),
        CONSTRAINT [FK_SubscriptionMembers_Subscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [Subscriptions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SubscriptionMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [Name])
    VALUES (1, N''Стрімінг відео''),
    (2, N''Музика''),
    (3, N''Програмне забезпечення''),
    (4, N''Хмарні сховища''),
    (5, N''Зв''''язок та інтернет''),
    (6, N''Освіта''),
    (7, N''Спорт і здоров''''я''),
    (8, N''Побутові послуги'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Email', N'Name') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] ON;
    EXEC(N'INSERT INTO [Users] ([Id], [CreatedAt], [Email], [Name])
    VALUES (1, ''2026-01-12T09:00:00Z'', N''o.kovalenko@example.com'', N''Олександр Коваленко''),
    (2, ''2026-01-12T09:00:00Z'', N''m.kovalenko@example.com'', N''Марія Коваленко''),
    (3, ''2026-02-03T09:00:00Z'', N''a.shevchenko@example.com'', N''Андрій Шевченко''),
    (4, ''2026-03-18T09:00:00Z'', N''i.bondar@example.com'', N''Ірина Бондар''),
    (5, ''2026-04-07T09:00:00Z'', N''t.melnyk@example.com'', N''Тарас Мельник'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Email', N'Name') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Categories_Name] ON [Categories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentMethods_UserId] ON [PaymentMethods] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PaymentMethodId] ON [Payments] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_Subscription_ChargedAt] ON [Payments] ([SubscriptionId], [ChargedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SubscriptionMembers_UserId] ON [SubscriptionMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_CategoryId] ON [Subscriptions] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_NextChargeDate] ON [Subscriptions] ([NextChargeDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_PaymentMethodId] ON [Subscriptions] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Subscriptions_Owner_Name] ON [Subscriptions] ([OwnerId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020125327_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020125327_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020130236_AddTrialEndDateAndStatusIndex'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [TrialEndDate] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020130236_AddTrialEndDateAndStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_Status_NextChargeDate] ON [Subscriptions] ([Status], [NextChargeDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020130236_AddTrialEndDateAndStatusIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020130236_AddTrialEndDateAndStatusIndex', N'10.0.13');
END;

COMMIT;
GO

