import { defineConfig } from "tsup";

export default defineConfig({
  entry: {
    index: "src/index.ts",
    axios: "src/axios.ts",
    react: "src/react.tsx",
    replay: "src/replay.ts",
  },
  format: ["esm", "cjs"],
  dts: true,
  sourcemap: true,
  clean: true,
  splitting: false,
  treeshake: true,
});
