using System;
using System.IO;
using System.Text;
using System.Web.Hosting;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using bufinsweb.Helpers;
using bufinsweb.Models;

namespace bufinsweb.Services
{
    /// <summary>
    /// Servicio para el envío de correos electrónicos usando MailKit
    /// </summary>
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _adminEmail;
        private readonly bool _enableSsl;

        public EmailService()
        {
            _smtpHost = EnvironmentHelper.GetRequiredVariable("SMTP_HOST");
            _smtpPort = EnvironmentHelper.GetIntVariable("SMTP_PORT", 587);
            _smtpUser = EnvironmentHelper.GetRequiredVariable("SMTP_USER");
            _smtpPassword = EnvironmentHelper.GetRequiredVariable("SMTP_PASSWORD");
            _fromEmail = EnvironmentHelper.GetRequiredVariable("SMTP_FROM_EMAIL");
            _adminEmail = EnvironmentHelper.GetRequiredVariable("ADMIN_EMAIL");
            _enableSsl = EnvironmentHelper.GetBooleanVariable("SMTP_ENABLE_SSL", true);
        }

        /// <summary>
        /// Envía el formulario de contacto al administrador
        /// </summary>
        public void SendContactFormToAdmin(ContactFormModel model)
        {
            string subject = $"📧 Nuevo mensaje de contacto - {model.Nombre}";

            string body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Nuevo mensaje de contacto</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f4f4f7;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f7; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <!-- Main Container -->
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);"">

                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #160933 0%, #2d1555 100%); padding: 40px 30px; text-align: center;"">
                            <img src=""cid:bufinslogo"" alt=""Bufins Logo"" style=""max-width: 200px; height: auto; margin-bottom: 15px; display: block; margin-left: auto; margin-right: auto;"">
                            <p style=""margin: 10px 0 0 0; color: #a8a8ff; font-size: 12px; letter-spacing: 1.5px; font-weight: 500;"">BUSINESS. FINANCE. ALWAYS. EVERYWHERE.</p>
                        </td>
                    </tr>

                    <!-- Alert Badge -->
                    <tr>
                        <td style=""padding: 30px 30px 0 30px; text-align: center;"">
                            <div style=""display: inline-block; background-color: #e8f5e9; color: #2e7d32; padding: 8px 20px; border-radius: 20px; font-size: 13px; font-weight: 600;"">
                                ✓ Nuevo mensaje recibido
                            </div>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style=""padding: 30px;"">
                            <h2 style=""margin: 0 0 25px 0; color: #160933; font-size: 24px; font-weight: 600;"">Detalles del contacto</h2>

                            <!-- Contact Info Card -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f8f9fa; border-radius: 8px; margin-bottom: 25px;"">
                                <tr>
                                    <td style=""padding: 20px;"">
                                        <table width=""100%"" cellpadding=""8"" cellspacing=""0"">
                                            <tr>
                                                <td width=""30%"" style=""color: #160933; font-weight: 600; font-size: 14px;"">👤 Nombre:</td>
                                                <td style=""color: #333333; font-size: 14px;"">{model.Nombre}</td>
                                            </tr>
                                            {(!string.IsNullOrWhiteSpace(model.Empresa) ? $@"
                                            <tr>
                                                <td style=""color: #160933; font-weight: 600; font-size: 14px;"">🏢 Empresa:</td>
                                                <td style=""color: #333333; font-size: 14px;"">{model.Empresa}</td>
                                            </tr>" : "")}
                                            <tr>
                                                <td style=""color: #160933; font-weight: 600; font-size: 14px;"">📧 Email:</td>
                                                <td style=""color: #333333; font-size: 14px;""><a href=""mailto:{model.Email}"" style=""color: #5e35b1; text-decoration: none;"">{model.Email}</a></td>
                                            </tr>
                                            {(!string.IsNullOrWhiteSpace(model.Telefono) ? $@"
                                            <tr>
                                                <td style=""color: #160933; font-weight: 600; font-size: 14px;"">📱 Teléfono:</td>
                                                <td style=""color: #333333; font-size: 14px;"">{model.Telefono}</td>
                                            </tr>" : "")}
                                        </table>
                                    </td>
                                </tr>
                            </table>

                            <!-- Message Card -->
                            <div style=""background-color: #f8f9fa; border-left: 4px solid #160933; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
                                <h3 style=""margin: 0 0 15px 0; color: #160933; font-size: 16px; font-weight: 600;"">💬 Mensaje:</h3>
                                <p style=""margin: 0; color: #333333; font-size: 14px; line-height: 1.6; white-space: pre-wrap;"">{model.Mensaje}</p>
                            </div>

                            <!-- Quick Actions -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-top: 30px;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""mailto:{model.Email}"" style=""display: inline-block; background: linear-gradient(135deg, #160933 0%, #2d1555 100%); color: #ffffff; text-decoration: none; padding: 14px 30px; border-radius: 8px; font-weight: 600; font-size: 14px; box-shadow: 0 4px 12px rgba(22,9,51,0.3);"">
                                            Responder ahora
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8f9fa; padding: 25px 30px; border-top: 1px solid #e0e0e0;"">
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""center"">
                                        <p style=""margin: 0 0 8px 0; color: #666666; font-size: 12px;"">
                                            <strong>Bufins</strong> • Business. Finance. Always. Everywhere.
                                        </p>
                                        <p style=""margin: 0; color: #999999; font-size: 11px;"">
                                            📧 admin@bufins.com • 🌐 www.bufins.com
                                        </p>
                                        <p style=""margin: 10px 0 0 0; color: #999999; font-size: 11px;"">
                                            Mensaje recibido el {DateTime.Now:dd/MM/yyyy} a las {DateTime.Now:HH:mm:ss}
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            SendEmail(_adminEmail, subject, body, true);
        }

        /// <summary>
        /// Envía un correo de confirmación al usuario que llenó el formulario
        /// </summary>
        public void SendConfirmationToUser(ContactFormModel model)
        {
            string subject = "✓ Hemos recibido tu mensaje - Bufins";

            string body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Confirmación de mensaje recibido</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f4f4f7;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f7; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <!-- Main Container -->
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);"">

                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #160933 0%, #2d1555 100%); padding: 50px 30px; text-align: center;"">
                            <img src=""cid:bufinslogo"" alt=""Bufins Logo"" style=""max-width: 220px; height: auto; margin-bottom: 15px; display: block; margin-left: auto; margin-right: auto;"">
                            <p style=""margin: 15px 0 0 0; color: #a8a8ff; font-size: 13px; letter-spacing: 1.5px; font-weight: 500;"">BUSINESS. FINANCE. ALWAYS. EVERYWHERE.</p>
                        </td>
                    </tr>

                    <!-- Success Icon -->
                    <tr>
                        <td style=""padding: 40px 30px 20px 30px; text-align: center;"">
                            <div style=""width: 80px; height: 80px; margin: 0 auto; background: linear-gradient(135deg, #4caf50 0%, #45a049 100%); border-radius: 50%; display: flex; align-items: center; justify-content: center; box-shadow: 0 6px 20px rgba(76,175,80,0.3);"">
                                <span style=""font-size: 48px; color: #ffffff; line-height: 80px;"">✓</span>
                            </div>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style=""padding: 0 30px 30px 30px; text-align: center;"">
                            <h2 style=""margin: 0 0 20px 0; color: #160933; font-size: 28px; font-weight: 600;"">¡Mensaje recibido!</h2>
                            <p style=""margin: 0 0 25px 0; color: #555555; font-size: 16px; line-height: 1.6;"">
                                Hola <strong style=""color: #160933;"">{model.Nombre}</strong>,
                            </p>
                            <p style=""margin: 0 0 30px 0; color: #666666; font-size: 15px; line-height: 1.7;"">
                                Gracias por contactarnos. Hemos recibido tu mensaje y nuestro equipo lo revisará a la brevedad. Te responderemos pronto a <strong style=""color: #5e35b1;"">{model.Email}</strong>
                            </p>

                            <!-- Message Summary Card -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f8f9fa; border-radius: 12px; margin-bottom: 30px;"">
                                <tr>
                                    <td style=""padding: 25px;"">
                                        <h3 style=""margin: 0 0 15px 0; color: #160933; font-size: 16px; font-weight: 600; text-align: left;"">📝 Tu mensaje:</h3>
                                        <div style=""background-color: #ffffff; border-radius: 8px; padding: 18px; text-align: left; border-left: 3px solid #160933;"">
                                            <p style=""margin: 0; color: #333333; font-size: 14px; line-height: 1.6; white-space: pre-wrap;"">{model.Mensaje}</p>
                                        </div>
                                    </td>
                                </tr>
                            </table>

                            <!-- Info Box -->
                            <div style=""background: linear-gradient(135deg, #e3f2fd 0%, #f3e5f5 100%); border-radius: 12px; padding: 25px; margin-bottom: 30px;"">
                                <p style=""margin: 0 0 15px 0; color: #160933; font-size: 15px; font-weight: 600;"">⏱️ ¿Cuándo te contactaremos?</p>
                                <p style=""margin: 0; color: #555555; font-size: 14px; line-height: 1.6;"">
                                    Nuestro equipo suele responder en un plazo de <strong>24 a 48 horas hábiles</strong>. Si tu consulta es urgente, puedes escribirnos directamente a <a href=""mailto:admin@bufins.com"" style=""color: #5e35b1; text-decoration: none; font-weight: 600;"">admin@bufins.com</a>
                                </p>
                            </div>

                            <!-- CTA Button -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""center"" style=""padding: 10px 0;"">
                                        <a href=""https://www.bufins.com"" style=""display: inline-block; background: linear-gradient(135deg, #160933 0%, #2d1555 100%); color: #ffffff; text-decoration: none; padding: 16px 40px; border-radius: 8px; font-weight: 600; font-size: 15px; box-shadow: 0 4px 12px rgba(22,9,51,0.3);"">
                                            Visitar nuestro sitio web
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Divider -->
                    <tr>
                        <td style=""padding: 0 30px;"">
                            <div style=""border-top: 2px solid #e0e0e0;""></div>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""padding: 30px; text-align: center; background-color: #fafafa;"">
                            <h4 style=""margin: 0 0 15px 0; color: #160933; font-size: 18px; font-weight: 600;"">BUFINS</h4>
                            <p style=""margin: 0 0 10px 0; color: #666666; font-size: 13px; font-weight: 500;"">
                                Business. Finance. Always. Everywhere.
                            </p>
                            <p style=""margin: 0 0 15px 0; color: #999999; font-size: 12px; line-height: 1.6;"">
                                📧 admin@bufins.com<br>
                                📱 +57 312 478 1486<br>
                                🌐 www.bufins.com
                            </p>
                            <p style=""margin: 15px 0 0 0; color: #999999; font-size: 11px;"">
                                © {DateTime.Now.Year} Bufins. Todos los derechos reservados.
                            </p>
                            <p style=""margin: 5px 0 0 0; color: #cccccc; font-size: 10px;"">
                                Este es un correo automático, por favor no responder.
                            </p>
                        </td>
                    </tr>
                </table>

                <!-- Spacer -->
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""margin-top: 20px;"">
                    <tr>
                        <td align=""center"">
                            <p style=""margin: 0; color: #999999; font-size: 11px;"">
                                Si no solicitaste este correo, puedes ignorarlo de forma segura.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            SendEmail(model.Email, subject, body, true);
        }

        /// <summary>
        /// Método privado para enviar correos usando MailKit
        /// </summary>
        private void SendEmail(string to, string subject, string body, bool isHtml = false)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Bufins", _fromEmail));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            if (isHtml)
            {
                bodyBuilder.HtmlBody = body;

                // Adjuntar el logo con Content-ID
                try
                {
                    string logoPath = HostingEnvironment.MapPath("~/Assets/images/Bufins_Logofinal.png");
                    if (File.Exists(logoPath))
                    {
                        var image = bodyBuilder.LinkedResources.Add(logoPath);
                        image.ContentId = "bufinslogo";
                    }
                }
                catch
                {
                    // Si no se puede adjuntar el logo, continuar sin él
                }
            }
            else
            {
                bodyBuilder.TextBody = body;
            }

            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                try
                {
                    SecureSocketOptions secureOptions;

                    if (_smtpPort == 465)
                    {
                        secureOptions = SecureSocketOptions.SslOnConnect;
                    }
                    else if (_smtpPort == 587)
                    {
                        secureOptions = SecureSocketOptions.StartTls;
                    }
                    else
                    {
                        secureOptions = _enableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
                    }

                    client.Connect(_smtpHost, _smtpPort, secureOptions);
                    client.Authenticate(_smtpUser, _smtpPassword);
                    client.Send(message);
                    client.Disconnect(true);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error al enviar correo: {ex.Message}", ex);
                }
            }
        }
    }
}
