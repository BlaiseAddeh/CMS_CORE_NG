using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelService
{
    public class TokenModel
    {
        [Key]
        public int Id { get; set; }

        // The ClientId, where it commes from
        [Required]
        public string ClientId { get; set; }

        //Value of the Token
        [Required]
        public string Value { get; set; }

        //Get the Token Creation Date
        [Required]
        public DateTime CreatedDate { get; set; }

        //The userId it was issued to
        // L'ID utilisateur auquel il a été attribué
        [Required]
        public string UserId { get; set; }

        [Required]
        public string LastModifiedDate { get; set; }

        [Required]
        public DateTime ExpiryTime { get; set; }

        [Required]
        public string EncryptionKeyRt { get; set; }

        [Required]
        public string EncryptionKeyJwt { get; set; }


        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } // Ne voulant pas créer cette colonne en base nous la déclaront virtual
    }
}
