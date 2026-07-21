// Product has no persisted Slug column (only Category does — Story 1.3's schema), and adding one
// would need a migration this environment can't verify against the real, unreachable target SQL
// Server instance. The backend's own contract is ID-based anyway (GET /api/v1/products/{id}), so
// the ID has to reach the frontend regardless of what the URL looks like — building it as
// {slugified-name}-{full-guid} (a common real-world e-commerce pattern) needs no backend slug
// field, no resolution endpoint, and no collision risk (GUIDs are unique by construction). The
// human-readable prefix is purely decorative; only the trailing GUID is ever parsed back out.
const GUID_PATTERN = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// Unicode combining diacritical marks (U+0300–U+036F) — what's left over after NFD-normalizing an
// accented character like "é" into "e" + a separate combining accent codepoint. Built via
// String.fromCharCode/RegExp rather than a literal character-range regex to avoid any editor/tool
// mangling raw combining-mark bytes embedded directly in source.
const COMBINING_DIACRITICS_PATTERN = new RegExp(
  `[${String.fromCharCode(0x0300)}-${String.fromCharCode(0x036f)}]`,
  'g',
);

export function slugify(text: string): string {
  return text
    .toLowerCase()
    .normalize('NFD')
    .replace(COMBINING_DIACRITICS_PATTERN, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-+|-+$)/g, '');
}

export function buildProductUrl(categorySlug: string, productId: string, productName: string): string[] {
  return ['/catalogue', categorySlug, `${slugify(productName)}-${productId}`];
}

export function extractProductIdFromSlug(slugSegment: string | null): string | null {
  if (!slugSegment) {
    return null;
  }
  const match = slugSegment.match(GUID_PATTERN);
  return match ? match[0] : null;
}
