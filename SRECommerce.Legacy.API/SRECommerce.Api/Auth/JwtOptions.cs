namespace SRECommerce.Api.Auth
{
    public class JwtOptions
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Issuer { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required]
        public string Audience { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required]
        public string Key { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Range(5, 1440)]
        public int ExpiresMinutes { get; set; }

        public void Validate()
        {
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(this,
                new System.ComponentModel.DataAnnotations.ValidationContext(this), validateAllProperties: true);
            if (System.Text.Encoding.UTF8.GetByteCount(Key) < 32)
                throw new System.ComponentModel.DataAnnotations.ValidationException(
                    "Jwt:Key must contain at least 32 UTF-8 bytes. Use user-secrets or a deployment secret provider.");
        }
    }
}

