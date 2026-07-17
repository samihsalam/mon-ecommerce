export default async (req, res) => {
  const { default: app } = await import('../dist/mon-ecommerce-web/server/server.mjs');
  return app(req, res);
};
