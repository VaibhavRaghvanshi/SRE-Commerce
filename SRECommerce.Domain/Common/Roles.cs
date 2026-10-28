namespace SRECommerce.Domain.Common
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Employee = "Employee";
        public const string Customer = "Customer";
        public const string Staff = Admin + "," + Employee;
    }
}

