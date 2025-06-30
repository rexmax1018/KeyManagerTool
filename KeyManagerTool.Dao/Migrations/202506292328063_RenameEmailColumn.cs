namespace KeyManagerTool.Dao.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class RenameEmailColumn : DbMigration
    {
        public override void Up()
        {
            RenameColumn("dbo.Customers", "EncryptedEmail", "Email"); // EF 自動生成的指令
        }

        public override void Down()
        {
            RenameColumn("dbo.Customers", "Email", "EncryptedEmail"); // 回滾指令
        }
    }
}