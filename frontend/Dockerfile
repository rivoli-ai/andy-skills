# Skill Registry SPA — same-origin `/api` is proxied to the API container by nginx
FROM node:22-alpine AS build
WORKDIR /app

ARG SKILL_PUBLIC_ORIGIN=http://localhost:8080

COPY package.json package-lock.json ./
RUN npm ci

COPY . ./
RUN printf '%s\n' "{\"apiUrl\":\"\",\"cliRegistryUrl\":\"${SKILL_PUBLIC_ORIGIN}\"}" > src/assets/config/config.json
RUN npm run build -- --configuration production

FROM nginx:alpine
COPY nginx/docker-default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 80
