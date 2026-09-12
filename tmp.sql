UPDATE users SET "EmailConfirmed" = true WHERE "Email" = 'cliente.novo.1610409668@teste.local';
SELECT "Email", "EmailConfirmed", tenant_id FROM users WHERE "Email" = 'cliente.novo.1610409668@teste.local';
