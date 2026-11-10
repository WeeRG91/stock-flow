import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { readFileSync } from "node:fs";
import { Agent as HttpsAgent } from "node:https";
import { homedir } from "node:os";
import { join } from "node:path";
import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";

export default defineConfig(({ command }) => {
  const config = {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
  };

  if (command !== "serve") {
    return config;
  }

  const certificateDirectory = join(homedir(), ".aspnet", "https");

  const certificate = readFileSync(
    join(certificateDirectory, "stockflow-vite.pem"),
  );

  const privateKey = readFileSync(
    join(certificateDirectory, "stockflow-vite.key"),
  );

  const apiAgent = new HttpsAgent({
    ca: certificate,
  });

  return {
    ...config,
    server: {
      host: "localhost",
      port: 5173,
      strictPort: true,
      https: {
        cert: certificate,
        key: privateKey,
      },
      proxy: {
        "/api": {
          target: "https://localhost:7167",
          changeOrigin: true,
          secure: true,
          agent: apiAgent,
        },
        "/hubs": {
          target: "https://localhost:7167",
          changeOrigin: true,
          secure: true,
          agent: apiAgent,
          ws: true,
        },
      },
    },
  };
});