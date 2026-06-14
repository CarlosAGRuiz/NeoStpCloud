using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Shared;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class DteControllerMobileContractTests
{
    private static (DteController Ctrl, IDteDocumentosService Docs, IConnectDteService Connect) Build(int? empresaId = 5)
    {
        var docs = Substitute.For<IDteDocumentosService>();
        var connect = Substitute.For<IConnectDteService>();
        var user = Substitute.For<ICurrentUser>();
        user.EmpresaId.Returns(empresaId);
        user.Username.Returns("mobile-admin");

        var ctrl = new DteController(docs, connect, user)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        return (ctrl, docs, connect);
    }

    [Theory]
    [InlineData(nameof(DteController.List))]
    [InlineData(nameof(DteController.GetById))]
    public void ConsultaDte_UsaPermisoConsultar_NoEmitir(string methodName)
    {
        var method = typeof(DteController).GetMethod(methodName)!;

        var permiso = method.GetCustomAttributes<RequirePermisoAttribute>()
            .Should().ContainSingle().Subject;

        permiso.Codigo.Should().Be("DTE.Consultar");
        permiso.Policy.Should().Be($"{RequirePermisoAttribute.PolicyPrefix}DTE.Consultar");
    }

    [Fact]
    public async Task List_DelegaConTenantDelToken_YDevuelvePagedResultEnApiResponse()
    {
        var (ctrl, docs, _) = Build();
        var page = PagedResult<DteDocumentoListItemDto>.Create(
            [new DteDocumentoListItemDto { Id = 10, NumeroControl = "DTE-01", EstadoCodigo = "PROCESADO" }],
            total: 1,
            page: 1,
            pageSize: 20);
        docs.GetListAsync(5, Arg.Any<DteListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<DteDocumentoListItemDto>>.Ok(page));

        var result = await ctrl.List(new DteListQuery { Search = "DTE-01" }, empresaId: 99, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<PagedResult<DteDocumentoListItemDto>>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Data!.Items.Should().ContainSingle(x => x.Id == 10);
        await docs.Received(1).GetListAsync(5, Arg.Is<DteListQuery>(q => q.Search == "DTE-01"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_DelegaConTenantDelToken_YDevuelveApiResponse()
    {
        var (ctrl, docs, _) = Build();
        docs.GetByIdAsync(5, 10, Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(new DteDocumentoDto { Id = 10, EstadoCodigo = "PROCESADO" }));

        var result = await ctrl.GetById(10, empresaId: null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<DteDocumentoDto>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Data!.Id.Should().Be(10);
        await docs.Received(1).GetByIdAsync(5, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescargasPdfYJson_DevuelvenBytesCrudos_NoApiResponse()
    {
        var (ctrl, docs, _) = Build();
        docs.ObtenerArchivosAsync(5, 10, Arg.Any<CancellationToken>())
            .Returns(Result<DteArchivosDto>.Ok(new DteArchivosDto
            {
                PdfFileName = "dte.pdf",
                PdfContent = [0x25, 0x50, 0x44, 0x46],
                JsonFileName = "dte.json",
                JsonContent = "{\"ok\":true}",
                NumeroControl = "DTE-01",
            }));

        var pdf = await ctrl.DescargarPdf(10, empresaId: null, CancellationToken.None);
        var json = await ctrl.DescargarJson(10, empresaId: null, CancellationToken.None);

        var pdfFile = pdf.Should().BeOfType<FileContentResult>().Subject;
        pdfFile.ContentType.Should().Be("application/pdf");
        pdfFile.FileContents.Should().StartWith([0x25, 0x50, 0x44, 0x46]);

        var jsonFile = json.Should().BeOfType<FileContentResult>().Subject;
        jsonFile.ContentType.Should().Be("application/json");
        jsonFile.FileContents.Should().NotBeEmpty();
    }
}
