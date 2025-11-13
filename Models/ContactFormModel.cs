using System.ComponentModel.DataAnnotations;

namespace bufinsweb.Models
{
    /// <summary>
    /// Modelo para el formulario de contacto
    /// </summary>
    public class ContactFormModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres")]
        [Display(Name = "Nombre completo")]
        public string Nombre { get; set; }

        [StringLength(100, ErrorMessage = "El nombre de la empresa no puede exceder los 100 caracteres")]
        [Display(Name = "Empresa")]
        public string Empresa { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido")]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "El teléfono no es válido")]
        [Display(Name = "Teléfono")]
        public string Telefono { get; set; }

        [Required(ErrorMessage = "El mensaje es obligatorio")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "El mensaje debe tener entre 10 y 1000 caracteres")]
        [Display(Name = "Mensaje")]
        public string Mensaje { get; set; }
    }
}
