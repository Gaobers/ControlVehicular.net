using AutoMapper;
using ControlVehicular.Mappings;
using ControlVehicular.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using ControlVehicular.Services.Integraciones;
using System.Net;

// =====================================================
// CONFIGURACIÓN INICIAL
// =====================================================

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// CONTROLADORES Y ENDPOINTS
// =====================================================

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

// =====================================================
// CONFIGURACIÓN DE SWAGGER
// =====================================================

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Control Vehicular API",
        Version = "v1",
        Description = "API REST desarrollada con ASP.NET Core 8 y SQL Server"
    });

    // Seguridad JWT en Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT generado por la API Java."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =====================================================
// CONFIGURACIÓN DE SQL SERVER - SOMEE
// =====================================================

var connectionString = builder.Configuration
    .GetConnectionString("ControlVehicularConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No se ha configurado la conexión a SQL Server."
    );
}

builder.Services.AddDbContext<ControlVehicularDbContext>(
    options => options.UseSqlServer(connectionString)
);

// =====================================================
// CONFIGURACIÓN DE AUTOMAPPER
// =====================================================

builder.Services.AddAutoMapper(
    cfg => { },
    typeof(MappingProfile)
);

// =====================================================
// REGISTRO DE SERVICIOS
// =====================================================
//
//
////////// =====================================================
        // INTEGRACIÓN CON API JAVA
        // =====================================================

var javaApiUrl = builder.Configuration["JavaApi:BaseUrl"];

if (!Uri.TryCreate(
        javaApiUrl,
        UriKind.Absolute,
        out var javaUri
    ) || javaUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException(
        "La URL de la API Java debe ser una dirección HTTPS válida."
    );
}

builder.Services.AddHttpClient<
    IJavaApiService,
    JavaApiService
>(client =>
{
    client.BaseAddress = new Uri(
        javaUri.AbsoluteUri.TrimEnd('/') + "/"
    );

    client.Timeout = TimeSpan.FromSeconds(200);
});



// =====================================================
// CONFIGURACIÓN JWT COMPARTIDA CON JAVA
// =====================================================

var jwtSecret = builder.Configuration["JwtSetting:SecretKey"];

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "No se ha configurado JwtSetting:SecretKey."
    );
}

// Java utiliza una clave codificada en Base64.

byte[] keyBytes;

try
{
    keyBytes = Convert.FromBase64String(jwtSecret);
}
catch (FormatException)
{
    throw new InvalidOperationException(
        "La clave JWT debe estar codificada en Base64."
    );
}

if (keyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "La clave JWT debe tener al menos 32 bytes."
    );
}

// =====================================================
// AUTENTICACIÓN JWT
// =====================================================

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme
)
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;

    options.SaveToken = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Java no incluye Issuer ni Audience actualmente.
        ValidateIssuer = false,
        ValidateAudience = false,

        // Validación de firma.
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,

        IssuerSigningKey = new SymmetricSecurityKey(
            keyBytes
        ),

        // Algoritmos HMAC compatibles con Java.
        ValidAlgorithms = new[]
        {
            SecurityAlgorithms.HmacSha256,
            SecurityAlgorithms.HmacSha384,
            SecurityAlgorithms.HmacSha512
        },

        // Validación de expiración.
        ValidateLifetime = true,
        RequireExpirationTime = true,

        // Identificación del usuario.
        NameClaimType = "sub",

        // Roles enviados desde Java.
        RoleClaimType = "roles",

        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

// =====================================================
// AUTORIZACIÓN
// =====================================================

builder.Services.AddAuthorization();

// =====================================================
// CONSTRUCCIÓN DE LA APLICACIÓN
// =====================================================

var app = builder.Build();

// =====================================================
// SWAGGER
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "Control Vehicular API v1"
        );
    });
}

// =====================================================
// MIDDLEWARE
// =====================================================

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

// =====================================================
// CONTROLADORES
// =====================================================

app.MapControllers();

// =====================================================
// ENDPOINT: ESTADO DE LA API
// =====================================================

app.MapGet("/api/estado", () =>
{
    return Results.Ok(new
    {
        exito = true,
        mensaje = "Control Vehicular API funcionando correctamente",
        tecnologia = "ASP.NET Core 8",
        baseDatos = "SQL Server"
    });
})
.WithName("ConsultarEstado")
.WithTags("Sistema")
.AllowAnonymous();

// =====================================================
// ENDPOINT: VERIFICAR JWT DE JAVA
// =====================================================

app.MapGet(
    "/api/integracion/validar-jwt",
    (ClaimsPrincipal usuario) =>
    {
        var correo = usuario.Identity?.Name;

        var roles = usuario
            .FindAll("roles")
            .Select(r => r.Value)
            .ToList();

        return Results.Ok(new
        {
            exito = true,
            mensaje = "Token JWT validado correctamente",
            correo = correo,
            roles = roles
        });
    }
)
.RequireAuthorization()
.WithName("ValidarJwtJava")
.WithTags("Integracion");

// =====================================================
// ENDPOINT: VERIFICAR ROL ADMINISTRADOR
// =====================================================

app.MapGet(
    "/api/integracion/admin",
    (ClaimsPrincipal usuario) =>
    {
        return Results.Ok(new
        {
            exito = true,
            mensaje = "Acceso autorizado para administrador",
            correo = usuario.Identity?.Name
        });
    }
)
.RequireAuthorization(policy =>
    policy.RequireRole("ADMINISTRADOR")
)
.WithName("VerificarAdministrador")
.WithTags("Integracion");


// =====================================================
// INTEGRACIÓN: CONSULTAR GASTOS DESDE JAVA
// =====================================================

app.MapGet(
    "/api/integracion/gastos",
    async (
        HttpContext httpContext,
        IJavaApiService javaApiService,
        CancellationToken cancellationToken
    ) =>
    {
        // Obtener encabezado Authorization
        var authorization = httpContext.Request
            .Headers.Authorization.ToString();

        if (!authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.Unauthorized();
        }

        // Extraer JWT
        var token = authorization[7..].Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.Unauthorized();
        }

        try
        {
            // Consultar API Java
            var respuesta = await javaApiService
                .ObtenerGastosAsync(
                    token,
                    cancellationToken
                );

            // Token rechazado por Java
            if (respuesta.EstadoHttp ==
                HttpStatusCode.Unauthorized)
            {
                return Results.Unauthorized();
            }

            // Acceso denegado por Java
            if (respuesta.EstadoHttp ==
                HttpStatusCode.Forbidden)
            {
                return Results.Forbid();
            }

            // Java no encontró registros
            if (respuesta.EstadoHttp ==
                HttpStatusCode.NotFound)
            {
                return Results.NotFound(new
                {
                    mensaje = "Java devolvió 404. " +
                              "No hay gastos disponibles " +
                              "o el recurso no fue encontrado."
                });
            }

            // Error del servidor Java
            if ((int)respuesta.EstadoHttp >= 500)
            {
                return Results.Problem(
                    "La API Java presentó un error.",
                    statusCode: 502
                );
            }

            // Respuesta sin contenido
            if (string.IsNullOrEmpty(
                    respuesta.Contenido))
            {
                return Results.StatusCode(
                    (int)respuesta.EstadoHttp
                );
            }

            // Devolver JSON original de Java
            return Results.Content(
                respuesta.Contenido,
                respuesta.TipoContenido
                    ?? "application/json",
                statusCode: (int)respuesta.EstadoHttp
            );
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                "No fue posible comunicarse con la API Java.",
                statusCode: 502
            );
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(
                "La API Java excedió el tiempo de espera.",
                statusCode: 504
            );
        }
    }
)
.RequireAuthorization()
.WithName("ConsultarGastosJava")
.WithTags("Integracion");


app.Run();
