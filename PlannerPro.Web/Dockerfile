# PlannerPro web image — Angular 22 built to static files, served by nginx.
# BUILD CONTEXT = this folder (PlannerPro.Web):
#   docker build -t plannerproweb -f Dockerfile .

FROM node:22-alpine AS build
WORKDIR /src
# Restore deps first (cached until package files change).
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Production build applies the environment.prod.ts file-replacement (apiBaseUrl='').
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/nginx.conf
# Angular's application builder emits to dist/<project>/browser.
COPY --from=build /src/dist/PlannerPro.Web/browser /usr/share/nginx/html
EXPOSE 80
