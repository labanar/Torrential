FROM mcr.microsoft.com/playwright:v1.50.0-noble
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

RUN mkdir -p /app/screenshots

CMD ["npx", "playwright", "test"]
