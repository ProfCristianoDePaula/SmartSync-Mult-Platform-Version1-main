INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") VALUES ('01a0966f-68ab-78ef-9b5f-8aadfdec03f4', '01a04d6c-714d-7a70-a878-a55586dc391a') ON CONFLICT DO NOTHING;
SELECT "UserId", "RoleId" FROM "AspNetUserRoles" WHERE "UserId" = '01a0966f-68ab-78ef-9b5f-8aadfdec03f4';
