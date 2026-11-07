using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace bufinsweb
{
    /// <summary>
    /// Lo que antes hacían Application_Start / Application_End de Global.asax: iniciar la analítica anónima y precargar
    /// los datos de Pulso Bufins en un hilo de fondo (la primera visita ya los encuentra en memoria), y volcar la
    /// analítica pendiente al cerrar la aplicación.
    /// </summary>
    public class InicioEnSegundoPlano : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            bufinsweb.Services.UsoService.Iniciar();
            Task.Run(() =>
            {
                try { bufinsweb.Services.PulsoService.Precargar(); }
                catch (Exception) { /* si faltan los archivos, el error aparecerá al visitar /pulso-bufins */ }
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            bufinsweb.Services.UsoService.Vaciar();
            return Task.CompletedTask;
        }
    }
}
