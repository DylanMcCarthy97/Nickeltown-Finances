# Nickeltown Finance — Design System Style Guide

Premium commercial desktop finance application. Target feel: Microsoft 365, Power BI, JetBrains Rider, modern banking software.

## Principles

- Every screen is intentional. No admin-panel aesthetics.
- One design system. No one-off colours, fonts, or spacing.
- Subtle motion only (150–180 ms fade / slide).
- Fluent System Icons exclusively.
- Responsive from 1366×768 to 3840×2160, including Windows display scaling.

## Colour palette

| Token | Hex | Use |
| --- | --- | --- |
| Background | `#181A20` | Window / page canvas |
| Surface | `#232833` | Cards, panels |
| Sidebar | `#1E2530` | Navigation rail |
| Primary | `#2D8CFF` | Actions, active nav, profit |
| Success / Income | `#22C55E` | Positive money |
| Warning | `#F59E0B` | Caution badges |
| Danger / Expense | `#EF4444` | Destructive actions, expenses |
| Text primary | `#FFFFFF` | Titles, values |
| Text secondary | `#B0BAC5` | Supporting copy |
| Muted | `#7A8794` | Labels, captions |

Borders use `#2E3644` / `#262D3A` only. No muddy greys.

## Typography

Font stack: **Segoe UI Variable → Inter → Segoe UI**.

| Style | Size | Weight | Resource key |
| --- | --- | --- | --- |
| Display | 40 / 36 | Light / SemiBold | `DisplayTextStyle`, `HeroAmountStyle` |
| Page title | 24 | SemiBold | `PageTitleStyle` |
| Section title | 15 | SemiBold | `SectionTitleStyle` |
| Card title | 13 | SemiBold | `CardTitleStyle` |
| Body | 14 | Normal | `BodyTextStyle` |
| Caption | 12 | Normal | `CaptionTextStyle` |

Never invent ad-hoc font sizes.

## Spacing

8 px grid: 4, 8, 12, 16, 24, 32, 40, 48.

Page content padding: 24×16. Card padding: 20. Sidebar: 220 px (64 px collapsed).

## Components

Use controls under `NickeltownFinance.Controls` and styles in `Themes/DesignSystem.xaml`:

| Component | Style / control |
| --- | --- |
| Primary button | `PrimaryButtonStyle` |
| Secondary button | `SecondaryButtonStyle` |
| Danger button | `DangerButtonStyle` |
| Toolbar button | `ToolbarButtonStyle` |
| Money card | `MoneyCard` |
| Statistic card | `StatisticCard` |
| Report card | `ReportCard` |
| Search box | `SearchBar` |
| Dialog | `AppDialogWindow` |
| Toast | `SuccessNotificationHost` |
| Status badge | `StatusBadge` |
| Empty state | `EmptyState` |
| Loading | `LoadingScreen` |
| Data grid | `AppDataGrid` |
| Page chrome | `PageShell`, `SurfaceCard` |

## Shell layout

- **Sidebar** (220 px): club logo, app name, navigation, current user, version. Collapses to icons.
- **Top bar**: search, notifications, user, financial year. No clutter.
- **Footer**: status, FY badge, user identity.
- **Login**: centred card, large logo, minimal fields — Microsoft 365 calm.

## Logo

Transparent PNG, uniform stretch, no white rectangles, no distortion. Small professional branding only.

## Charts

LiveCharts2 — rounded bars, smooth animation, modern tooltips, palette colours only.
