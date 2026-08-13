using ArcanoPizza_API.Data.IServices;
using ArcanoPizza_API.DTOs;
using ArcanoPizza_API.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ArcanoPizza_API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Administrador,Tecnico")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public AdminController(IAdminService admin, IPasswordHasher<Usuario> passwordHasher)
    {
        _admin = admin;
        _passwordHasher = passwordHasher;
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> GetUsuarios()
    {
        var response = await _admin.GetUsuariosAsync();
        return Ok(response);
    }

    [HttpGet("usuarios/{id:int}")]
    public async Task<IActionResult> GetUsuario(int id)
    {
        var response = await _admin.GetUsuarioAsync(id);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("usuarios")]
    public async Task<IActionResult> CrearUsuario([FromBody] UsuarioAdminDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { mensaje = "La contraseña es obligatoria." });

        var hash = _passwordHasher.HashPassword(
            new Usuario { Correo = dto.Email },
            dto.Password);

        var (ok, error) = await _admin.CrearUsuarioAsync(dto, hash);
        if (error is not null)
            return StatusCode(500, new { mensaje = "Error al guardar usuario", error });

        return Ok(ok);
    }

    [HttpPut("usuarios/{id:int}")]
    public async Task<IActionResult> UpdateUsuario(int id, [FromBody] UsuarioUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        string? hash = null;
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            hash = _passwordHasher.HashPassword(
                new Usuario { Correo = dto.Email },
                dto.Password);
        }

        var (found, error) = await _admin.UpdateUsuarioAsync(id, dto, hash);
        if (!found) return NotFound();
        if (error is not null) return StatusCode(500, error);

        return NoContent();
    }

    [HttpPatch("usuarios/{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var ok = await _admin.ToggleUsuarioAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("usuarios/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var eliminado = await _admin.DeleteUsuarioAsync(id);
        return eliminado ? NoContent() : NotFound();
    }

    [HttpGet("productos")]
    public async Task<IActionResult> GetProductos()
    {
        var response = await _admin.GetProductosAsync();
        return Ok(response);
    }

    [HttpPost("productos")]
    public async Task<IActionResult> CrearProducto([FromBody] ProductoAdminDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var response = await _admin.CrearProductoAsync(dto);
        return Ok(response);
    }

    [HttpPut("productos/{id:int}")]
    public async Task<IActionResult> UpdateProducto(int id, [FromBody] ProductoUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (found, error) = await _admin.UpdateProductoAsync(id, dto);
        if (!found) return NotFound();
        if (error is not null) return StatusCode(500, error);

        return NoContent();
    }

    [HttpPatch("productos/{id:int}/toggle")]
    public async Task<IActionResult> ToggleProducto(int id)
    {
        var ok = await _admin.ToggleProductoAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("productos/{id:int}")]
    public async Task<IActionResult> DeleteProducto(int id)
    {
        var eliminado = await _admin.DeleteProductoAsync(id);
        return eliminado ? NoContent() : NotFound();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> ObtenerMetricasDashboard()
    {
        var (ok, error) = await _admin.ObtenerMetricasDashboardAsync();
        if (error is not null)
            return StatusCode(500, error);

        return Ok(ok);
    }
}
