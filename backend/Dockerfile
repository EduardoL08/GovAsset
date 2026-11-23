FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/GovAsset.Api/GovAsset.Api.csproj src/GovAsset.Api/
RUN dotnet restore src/GovAsset.Api/GovAsset.Api.csproj
COPY src/ src/
RUN dotnet publish src/GovAsset.Api/GovAsset.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# biblioteca Kerberos usada pelo driver do PostgreSQL (evita o aviso libgssapi_krb5.so.2 no log)
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Render/Heroku-like: respeita X-Forwarded-For (necessário para o limite de requisições por IP)
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet GovAsset.Api.dll"]
