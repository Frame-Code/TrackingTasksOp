# Un solo COPY en vez de copiar los .csproj primero para cachear el restore: Web.csproj
# arma la lista de .cs con una tarea inline de MSBuild que recorre el disco, así que un
# restore con los proyectos vacíos no representa el build real.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Web/Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0

# La imagen de aspnet no trae tzdata, y sin eso .NET ignora TZ y se queda en UTC. StartTime y
# EndTime significan "reloj de pared" (ver CLAUDE.md), asi que un contenedor en UTC registra
# las sesiones cinco horas corridas respecto de lo que el usuario ve en su reloj.
RUN apt-get update && apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

# El publish NO copia appsettings.json a propósito (así el deploy manual no pierde sus
# credenciales al republicar), pero un contenedor arranca vacío y sin él ni levanta.
# Se hornea el example, que trae la estructura completa sin secretos; lo sensible entra
# por variables de entorno (ver docker-compose.yml).
COPY Web/appsettings.example.json /app/appsettings.json

# ContentRootPath es /app, así que el key ring de Data Protection cae en /app/Keys y los
# logs en /app/logs. Keys va en un volumen (ver docker-compose.yml): si se pierde, las API
# keys de OpenProject guardadas quedan ilegibles y hay que cargarlas de nuevo.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Web.dll"]
