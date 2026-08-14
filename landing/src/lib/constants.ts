export const GITHUB_URL = "https://github.com/NguyenHuuKhanhDuy/Portix";
export const README_URL = `${GITHUB_URL}#readme`;
export const RELEASES_URL = `${GITHUB_URL}/releases`;

// Placeholder values shown in example commands/output across the landing page — edit these to
// match your own deployment.
export const EXAMPLE_SERVER_URL = "https://api.portix.io.vn:5100";
export const EXAMPLE_API_TOKEN = "your-api-token";
export const EXAMPLE_LOCAL_PORT = 3000;
export const EXAMPLE_TUNNEL_SUBDOMAIN = "a1b2c3d4";
export const EXAMPLE_TUNNEL_DOMAIN = "tunnel.portix.io.vn";

// GitHub redirects /releases/latest/download/<asset> to that asset on whichever release is
// currently latest — no version number to keep in sync with here.
const LATEST_RELEASE_BASE = `${GITHUB_URL}/releases/latest/download`;
export const DOWNLOAD_WIN_X64 = `${LATEST_RELEASE_BASE}/portix-win-x64.zip`;
export const DOWNLOAD_OSX_ARM64 = `${LATEST_RELEASE_BASE}/portix-osx-arm64.zip`;
export const DOWNLOAD_OSX_X64 = `${LATEST_RELEASE_BASE}/portix-osx-x64.zip`;
