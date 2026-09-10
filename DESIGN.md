# Design direction

SafeScreen uses a utilitarian high-contrast interface built around three
semantic roles:

- near-black surfaces keep attention on the boundary;
- cyan marks the usable area and the currently editable edge;
- scarlet hatching marks excluded pixels only.

The full-screen picker is the primary interface. Its controls stay in the
usable upper-right area, while large handles and keyboard alternatives make
the boundaries easy to adjust. Decorative elements never replace state labels.

The direction is grounded in native desktop utility conventions: high contrast,
visible keyboard focus, direct manipulation, large targets, and semantic color.
The interface intentionally avoids decorative gradients, card-heavy layouts,
and visual effects without a control meaning.

## Public product page

The GitHub presentation keeps the same visual grammar instead of introducing a
separate marketing theme. A synthetic monitor diagram carries the product proof;
no private desktop screenshot is published.

The repository structure adapts three public conventions:

- PowerToys' direct path from product purpose to installation and releases;
- EarTrumpet's concise Windows-utility positioning and supported-platform clarity;
- GitHub's native README, release, security, and structured issue surfaces.

The hero preserves SafeScreen's near-black structure, cyan usable rectangle,
and scarlet damaged-zone hatching. It deliberately excludes purple gradients,
generic feature-card grids, invented testimonials, and claims that are not
demonstrated by the current build.
