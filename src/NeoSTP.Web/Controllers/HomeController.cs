using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ICurrentUser _currentUser;

    public HomeController(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public IActionResult Index()
    {
        ViewBag.Username = _currentUser.Username;
        ViewBag.TipoUsuario = _currentUser.TipoUsuarioCodigo;
        ViewBag.Roles = _currentUser.Roles;
        ViewBag.Permisos = _currentUser.Permisos;
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
