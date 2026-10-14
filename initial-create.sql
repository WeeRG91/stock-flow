CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Products" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "Description" character varying(2000),
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Products" PRIMARY KEY ("Id")
);

CREATE TABLE "Warehouses" (
    "Id" uuid NOT NULL,
    "Code" character varying(32) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Location" character varying(300) NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Warehouses" PRIMARY KEY ("Id")
);

CREATE TABLE "Inventories" (
    "Id" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "QuantityOnHand" integer NOT NULL,
    CONSTRAINT "PK_Inventories" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Inventories_QuantityOnHand_NonNegative" CHECK ("QuantityOnHand" >= 0),
    CONSTRAINT "FK_Inventories_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Inventories_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "StockMovements" (
    "Id" uuid NOT NULL,
    "InventoryId" uuid NOT NULL,
    "QuantityChange" integer NOT NULL,
    "Type" integer NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_StockMovements_QuantityChange_NonZero" CHECK ("QuantityChange" <> 0),
    CONSTRAINT "CK_StockMovements_TypeAndSign" CHECK (("Type" = 1 AND "QuantityChange" > 0)
OR ("Type" = 2 AND "QuantityChange" < 0)
OR ("Type" = 3)),
    CONSTRAINT "FK_StockMovements_Inventories_InventoryId" FOREIGN KEY ("InventoryId") REFERENCES "Inventories" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Inventories_ProductId_WarehouseId" ON "Inventories" ("ProductId", "WarehouseId");

CREATE INDEX "IX_Inventories_WarehouseId" ON "Inventories" ("WarehouseId");

CREATE UNIQUE INDEX "IX_Products_Sku" ON "Products" ("Sku");

CREATE INDEX "IX_StockMovements_InventoryId" ON "StockMovements" ("InventoryId");

CREATE UNIQUE INDEX "IX_Warehouses_Code" ON "Warehouses" ("Code");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261014155407_InitialCreate', '10.0.4');

COMMIT;

