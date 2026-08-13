using ArcanoPizza_API.Data.Interface;
using ArcanoPizza_API.Data.IServices;
using ArcanoPizza_API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ArcanoPizza_API.Data.Services;

public class PedidosService : IPedidosService
{
    private readonly IPedidoRepository _pedidoRepository;
    private readonly IPedidoCreacionService _pedidoCreacion;
    private readonly ArcanoPizzaDbContext _db;

    public PedidosService(
        IPedidoRepository pedidoRepository,
        IPedidoCreacionService pedidoCreacion,
        ArcanoPizzaDbContext db)
    {
        _pedidoRepository = pedidoRepository;
        _pedidoCreacion = pedidoCreacion;
        _db = db;
    }

    public async Task<IReadOnlyList<PedidoListaDto>> MisPedidosAsync(int userId, CancellationToken ct)
    {
        var lista = await _pedidoRepository.GetByUsuarioAsync(userId, ct);
        return lista.Select(p => new PedidoListaDto(
            p.IdPedido,
            p.Estado,
            p.Total,
            p.TimeStamp ?? p.CreatedAt,
            p.TipoEntrega,
            p.Promocion?.Titulo,
            p.MetodoPago
        )).ToList();
    }

    public async Task<PedidoDetalleDto?> ObtenerDetalleAsync(int idPedido, int userId, CancellationToken ct)
    {
        var pedido = await _pedidoRepository.GetDetalleUsuarioAsync(idPedido, userId, ct);
        if (pedido is null) return null;

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

        return new PedidoDetalleDto(
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
            lineas);
    }

    public async Task<(PedidoDetalleDto? Detalle, string? Error, int? HttpStatusCode)> CrearAsync(
        int userId,
        PedidoCrearDto dto,
        CancellationToken ct)
    {
        var (detalle, error) = await _pedidoCreacion.CrearAsync(userId, dto, stripeCheckoutSessionId: null, ct);
        if (error is not null) return (null, error, 400);
        if (detalle is null) return (null, "No se pudo crear el pedido.", 500);
        return (detalle, null, null);
    }

    public async Task<IReadOnlyList<PedidoDashboardDto>> DashboardAsync(CancellationToken ct)
    {
        var pedidos = await _pedidoRepository.GetPedidosActivosDashboardAsync(ct);

        return pedidos.Select(p => new PedidoDashboardDto(
            Id: $"ORD-{p.IdPedido:D6}",
            Estado: p.Estado,
            TipoEntrega: p.TipoEntrega,
            Urgente: p.TipoEntrega.Equals("Express", StringComparison.OrdinalIgnoreCase),
            HoraRecibido: p.CreatedAt.ToString("HH:mm"),
            HoraEntrega: p.CreatedAt.AddMinutes(30).ToString("HH:mm"),
            Cliente: new ClienteResumenDto(
                Nombre: p.Usuario?.NombreUsuario ?? "Cliente Desconocido",
                Telefono: p.Usuario?.Telefono ?? "Sin teléfono",
                Direccion: p.Direccion is { } d
                    ? $"{d.Calle}, {d.Colonia}"
                    : "Recoger en local"
            ),
            Productos: p.PedidosItem.Select(pi => new ProductoResumenDto(
                Cantidad: pi.Cantidad,
                Nombre: pi.Producto?.Nombre ?? "(producto sin nombre)",
                Nota: null,
                Ingredientes: pi.Producto?.Ingredientes ?? "Sin ingredientes"
            )).ToList(),
            Total: p.Total
        )).ToList();
    }

    public async Task<IReadOnlyList<PedidoDashboardDto>> MisAsignadosAsync(int repartidorId, CancellationToken ct)
    {
        var pedidos = await _pedidoRepository.GetAsignadosARepartidorAsync(repartidorId, ct);

        return pedidos.Select(p => new PedidoDashboardDto(
            Id: $"ORD-{p.IdPedido:D6}",
            Estado: p.Estado,
            TipoEntrega: p.TipoEntrega,
            Urgente: p.TipoEntrega.Equals("Express", StringComparison.OrdinalIgnoreCase),
            HoraRecibido: p.CreatedAt.ToString("HH:mm"),
            HoraEntrega: p.CreatedAt.AddMinutes(30).ToString("HH:mm"),
            Cliente: new ClienteResumenDto(
                Nombre: p.Usuario?.NombreUsuario ?? "Cliente Desconocido",
                Telefono: p.Usuario?.Telefono ?? "Sin teléfono",
                Direccion: p.Direccion is { } d
                    ? $"{d.Calle}, {d.Colonia}"
                    : "Recoger en local"
            ),
            Productos: p.PedidosItem.Select(pi => new ProductoResumenDto(
                Cantidad: pi.Cantidad,
                Nombre: pi.Producto?.Nombre ?? "(producto sin nombre)",
                Nota: null,
                Ingredientes: pi.Producto?.Ingredientes ?? "Sin ingredientes"
            )).ToList(),
            Total: p.Total
        )).ToList();
    }

    public async Task<(bool Ok, string? Error, int? HttpStatusCode, int? MeseroId)> ActualizarEstadoAsync(
        int idPedido,
        string nuevoEstado,
        int? actingUserId,
        string? actingRole,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nuevoEstado))
            return (false, "El estado no puede estar vacío.", 400, null);

        var pedidoInfo = await _db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.IdPedido == idPedido, ct);
        if (pedidoInfo is null)
            return (false, $"No se encontró el pedido con ID {idPedido}", 404, null);

        if (string.Equals(actingRole, "Repartidor", StringComparison.OrdinalIgnoreCase))
        {
            if (actingUserId is null)
                return (false, "Token sin identificador de usuario válido.", 401, null);

            if (pedidoInfo.FkIdRepartidor != actingUserId.Value)
                return (false, "No autorizado para modificar este pedido.", 403, null);
        }

        // ====================================================================
        // VALIDACIÓN DE TRANSICIONES DE ESTADOS (Evita saltos ilógicos)
        // ====================================================================
        var estadoActual = pedidoInfo.Estado ?? "Pendiente";
        var transicionesPermitidas = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Pendiente", new List<string> { "En Preparación", "Cancelado" } },
            { "En Preparación", new List<string> { "Lista para entregar", "Cancelado" } },
            { "Lista para entregar", new List<string> { "Entregado", "Pagado", "Cancelado" } }
        };

        if (transicionesPermitidas.ContainsKey(estadoActual))
        {
            if (!transicionesPermitidas[estadoActual].Contains(nuevoEstado, StringComparer.OrdinalIgnoreCase))
            {
                return (false, $"Transición inválida. No se puede pasar de '{estadoActual}' a '{nuevoEstado}'.", 400, null);
            }
        }

        var exito = await _pedidoRepository.ActualizarEstadoAsync(idPedido, nuevoEstado, ct);
        if (!exito)
            return (false, $"Error al actualizar en base de datos el pedido {idPedido}", 500, null);

        int? meseroId = pedidoInfo.FkIdOperador ?? pedidoInfo.FkIdUsuario;
        return (true, null, null, meseroId);
    }

    public async Task<IReadOnlyList<EmpleadoResumenDto>> GetRepartidoresAsync(CancellationToken ct)
    {
        var empleados = await _db.Usuarios
            .Where(u => u.Rol == "Repartidor" && u.Activo)
            .Select(u => new EmpleadoResumenDto(u.IdUsuario, u.NombreUsuario))
            .ToListAsync(ct);

        return empleados;
    }

    public async Task<(bool Ok, string? Error, int? HttpStatusCode)> AsignarRepartidorAsync(
        int idPedido,
        int repartidorId,
        CancellationToken ct)
    {
        var pedido = await _db.Pedidos.FindAsync(new object[] { idPedido }, ct);
        if (pedido is null)
            return (false, "Pedido no encontrado", 404);

        if (!string.Equals(pedido.TipoEntrega, "Reparto", StringComparison.OrdinalIgnoreCase))
            return (false, "Solo se puede asignar repartidor a pedidos con tipoEntrega='Reparto'.", 400);

        pedido.Estado = "En Ruta";
        pedido.FkIdRepartidor = repartidorId;

        await _db.SaveChangesAsync(ct);
        return (true, null, null);
    }

    public async Task<IReadOnlyList<HistorialPedidoResponseDto>> ObtenerHistorialDiaAsync(CancellationToken ct)
    {
        var inicioDia = DateTime.UtcNow.Date;

        var pedidos = await _db.Pedidos
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.Operador)
            .Include(p => p.PedidosItem)
                .ThenInclude(pi => pi.Producto)
            // MODIFICADO: Solo jala las órdenes de tipo "Salon"
            .Where(p => p.CreatedAt >= inicioDia && p.TipoEntrega == "Salon")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return pedidos.Select(p => new HistorialPedidoResponseDto(
            p.IdPedido,
            p.Operador?.NombreUsuario ?? p.Usuario?.NombreUsuario ?? "Desconocido",
            p.CreatedAt,
            p.Estado,
            p.PedidosItem.Select(pi => $"{pi.Cantidad}x {(pi.Producto?.Nombre ?? "(producto)")}").ToList()
        )).ToList().AsReadOnly();
    }

    // NUEVO: Método para traer la cola de trabajo de órdenes activas (solo Salón)
    public async Task<IReadOnlyList<PedidoDashboardDto>> ObtenerActivasSalonAsync(CancellationToken ct)
    {
        // Se asume que las órdenes activas son las que no están terminadas
        var estadosInactivos = new[] { "Entregado", "Pagado", "Cancelado" };

        var pedidos = await _db.Pedidos
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.PedidosItem)
                .ThenInclude(pi => pi.Producto)
            .Where(p => p.TipoEntrega == "Salon" && !estadosInactivos.Contains(p.Estado))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        return pedidos.Select(p => new PedidoDashboardDto(
            Id: $"ORD-{p.IdPedido:D6}",
            Estado: p.Estado,
            TipoEntrega: p.TipoEntrega,
            Urgente: false,
            HoraRecibido: p.CreatedAt.ToString("HH:mm"),
            HoraEntrega: "",
            Cliente: new ClienteResumenDto(p.Usuario?.NombreUsuario ?? "Mesa Salón", "", ""),
            Productos: p.PedidosItem.Select(pi => new ProductoResumenDto(
                Cantidad: pi.Cantidad,
                Nombre: pi.Producto?.Nombre ?? "",
                Nota: null,
                Ingredientes: pi.Producto?.Ingredientes ?? ""
            )).ToList(),
            Total: p.Total
        )).ToList().AsReadOnly();
    }
}