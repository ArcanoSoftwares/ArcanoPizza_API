using ArcanoPizza_API.Data.Interface;
using ArcanoPizza_API.DTOs;
using ArcanoPizza_API.Helpers;
using ArcanoPizza_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcanoPizza_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PedidosController : ControllerBase
{
    private readonly IPedidoRepository _pedidoRepository;
    private readonly IPedidoCreacionService _pedidoCreacion;

    public PedidosController(IPedidoRepository pedidoRepository, IPedidoCreacionService pedidoCreacion)
    {
        _pedidoRepository = pedidoRepository;
        _pedidoCreacion = pedidoCreacion;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PedidoListaDto>>> MisPedidos(CancellationToken ct)
    {
        var userId = User.GetUsuarioId();
        var lista = await _pedidoRepository.GetByUsuarioAsync(userId, ct);

        // 🔥 CORRECCIÓN: Le pasamos exactamente los 6 datos que espera el PedidoListaDto
        var dto = lista.Select(p => new PedidoListaDto(
                p.IdPedido,
                p.Estado,
                p.Total,
                p.TimeStamp ?? DateTime.UtcNow,
                p.TipoEntrega,
                p.Promocion?.Titulo
            ))
            .ToList();

        return Ok(dto);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PedidoDetalleDto>> Obtener(int id, CancellationToken ct)
    {
        var userId = User.GetUsuarioId();
        var pedido = await _pedidoRepository.GetDetalleUsuarioAsync(id, userId, ct);
        if (pedido is null)
            return NotFound();

        var lineas = pedido.PedidosItem.Select(i => new PedidoLineaDetalleDto(
                i.IdPedidoItem,
                i.Producto?.Nombre ?? "(producto)",
                i.Cantidad,
                i.PrecioUnitario,
                i.TamanoPizza?.Nombre))
            .ToList();

        var direccion = pedido.Direccion is { } d
            ? new DireccionDto(d.IdDireccion, d.Calle, d.Colonia, d.CodigoPostal)
            : new DireccionDto(0, "Recoger en local", "—", "—");

        return Ok(new PedidoDetalleDto(
            pedido.IdPedido,
            pedido.Estado,
            pedido.Subtotal,
            pedido.DescuentoTotal,
            pedido.Impuestos,
            pedido.Total,
            pedido.TipoEntrega,
            pedido.TimeStamp,
            pedido.FkIdPromocion,
            pedido.Promocion?.Titulo,
            pedido.MetodoPago,
            direccion,
            lineas));
    }

    [HttpPost]
    public async Task<ActionResult<PedidoDetalleDto>> Crear([FromBody] PedidoCrearDto? dto, CancellationToken ct)
    {
        if (dto is null)
            return BadRequest("Cuerpo de pedido inválido o vacío.");

        int userId;
        try
        {
            userId = User.GetUsuarioId();
        }
        catch (InvalidOperationException)
        {
            return Unauthorized("Token sin identificador de usuario válido.");
        }

        var (detalle, error) = await _pedidoCreacion.CrearAsync(userId, dto, stripeCheckoutSessionId: null, ct);
        if (error is not null)
            return BadRequest(error);
        if (detalle is null)
            return Problem("No se pudo crear el pedido.");

        // Evita InvalidOperationException si el enrutador no resuelve CreatedAtAction.
        var location = $"/api/Pedidos/{detalle.IdPedido}";
        return Created(location, detalle);
    }
<<<<<<< Updated upstream
=======

    // GET: api/Pedidos/dashboard
    [HttpGet("dashboard")]
    [Authorize(Roles = "Empleado,Administrador,Tecnico")]
    public async Task<ActionResult<IReadOnlyList<PedidoDashboardDto>>> GetDashboard(CancellationToken ct)
    {
        var dto = await _pedidos.DashboardAsync(ct);
        return Ok(dto);
    }

    // GET: api/Pedidos/mis-asignados
    [HttpGet("mis-asignados")]
    [Authorize(Roles = "Repartidor")]
    public async Task<ActionResult<IReadOnlyList<PedidoDashboardDto>>> MisAsignados(CancellationToken ct)
    {
        var userId = User.GetUsuarioId();
        var dto = await _pedidos.MisAsignadosAsync(userId, ct);
        return Ok(dto);
    }

    [HttpPatch("{id:int}/estado")]
    [Authorize(Roles = "Empleado,Administrador,Tecnico,Repartidor")]
    public async Task<IActionResult> ActualizarEstado(int id, [FromBody] string nuevoEstado, CancellationToken ct)
    {
        int? actingUserId = null;
        try { actingUserId = User.GetUsuarioId(); } catch { /* ignore */ }
        var rol = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

        // MODIFICADO: Extraemos también el meseroId de la tupla
        var (ok, error, status, meseroId) = await _pedidos.ActualizarEstadoAsync(id, nuevoEstado, actingUserId, rol, ct);
        if (!ok)
            return status switch
            {
                400 => BadRequest(new { mensaje = error }),
                401 => Unauthorized(new { mensaje = error }),
                403 => Forbid(),
                404 => NotFound(new { mensaje = error }),
                _ => Problem(error),
            };

        // LÓGICA PARA NOTIFICAR AL MESERO (Y A SU RELOJ INTELIGENTE)
        if (string.Equals(nuevoEstado, "Lista para entregar", StringComparison.OrdinalIgnoreCase) && meseroId.HasValue)
        {
            // NOTA: Cuando tu compañero termine de configurar el servicio Push (FCM / SignalR), 
            // la llamada debe ir justo aquí. Por ahora está comentada para no romper la compilación.
            // 
            // await _notificacionService.EnviarNotificacionPushAsync(
            //     usuarioId: meseroId.Value, 
            //     titulo: $"Pedido #{id} Listo", 
            //     cuerpo: "El despachador ha marcado la orden como lista para entregar."
            // );
        }

        return Ok(new { mensaje = "Estado actualizado correctamente", estadoAsignado = nuevoEstado });
    }

    // GET: api/Pedidos/repartidores
    [HttpGet("repartidores")]
    public async Task<ActionResult<IEnumerable<EmpleadoResumenDto>>> GetRepartidores(CancellationToken ct)
    {
        var empleados = await _pedidos.GetRepartidoresAsync(ct);
        return Ok(empleados);
    }

    // PATCH: api/Pedidos/{id}/asignar-repartidor
    [HttpPatch("{id:int}/asignar-repartidor")]
    [Authorize(Roles = "Empleado,Administrador,Tecnico")]
    public async Task<IActionResult> AsignarRepartidor(int id, [FromBody] AsignarRepartidorRequest request, CancellationToken ct)
    {
        var (ok, error, status) = await _pedidos.AsignarRepartidorAsync(id, request.RepartidorId, ct);
        if (!ok)
            return status switch
            {
                400 => BadRequest(new { mensaje = error }),
                404 => NotFound(new { mensaje = error }),
                _ => Problem(error),
            };

        return Ok(new { mensaje = "Repartidor asignado correctamente" });
    }

    // GET: api/Pedidos/historial-despachador
    [HttpGet("historial-despachador")]
    [Authorize(Roles = "Empleado,Administrador,Tecnico")] // Asumo que el despachador entra en "Empleado"
    public async Task<ActionResult<IReadOnlyList<HistorialPedidoResponseDto>>> ObtenerHistorialDespachador(CancellationToken ct)
    {
        var historial = await _pedidos.ObtenerHistorialDiaAsync(ct);
        return Ok(historial);
    }
>>>>>>> Stashed changes
}