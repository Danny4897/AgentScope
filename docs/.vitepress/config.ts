import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'AgentScope',
  description: 'AI agent observability platform for .NET — see every agent, trace every pipeline, catch every failure.',
  base: '/AgentScope/',
  cleanUrls: true,
  ignoreDeadLinks: true,

  head: [
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { name: 'twitter:card', content: 'summary' }],
  ],

  themeConfig: {
    logo: '/logo.svg',
    siteTitle: 'AgentScope',

    nav: [
      {
        text: 'Guide',
        items: [
          { text: 'Getting Started', link: '/getting-started' },
          { text: 'Architecture', link: '/architecture' },
          { text: 'Deploy', link: '/deploy' },
        ],
      },
      {
        text: 'Features',
        items: [
          { text: 'Pipeline Tracing', link: '/features/tracing' },
          { text: 'Metrics Dashboard', link: '/features/metrics' },
          { text: 'Circuit Breakers', link: '/features/circuit-breaker' },
          { text: 'Alerts', link: '/features/alerts' },
        ],
      },
      {
        text: 'Integrations',
        items: [
          { text: 'MonadicSharp.Telemetry', link: '/integrations/telemetry' },
          { text: 'Semantic Kernel', link: '/integrations/semantic-kernel' },
          { text: 'Microsoft.Extensions.AI', link: '/integrations/extensions-ai' },
        ],
      },
      {
        text: 'Ecosystem',
        items: [
          {
            text: 'Core',
            items: [
              { text: 'MonadicSharp', link: 'https://danny4897.github.io/MonadicSharp/' },
              { text: 'MonadicSharp.Framework', link: 'https://danny4897.github.io/MonadicSharp.Framework/' },
            ],
          },
          {
            text: 'Extensions',
            items: [
              { text: 'MonadicSharp.AI', link: 'https://danny4897.github.io/MonadicSharp.AI/' },
              { text: 'MonadicSharp.Recovery', link: 'https://danny4897.github.io/MonadicSharp.Recovery/' },
              { text: 'MonadicSharp.Azure', link: 'https://danny4897.github.io/MonadicSharp.Azure/' },
              { text: 'MonadicSharp.DI', link: 'https://danny4897.github.io/MonadicSharp.DI/' },
            ],
          },
          {
            text: 'Tooling',
            items: [
              { text: 'MonadicLeaf', link: 'https://danny4897.github.io/MonadicLeaf/' },
              { text: 'MonadicSharp × OpenCode', link: 'https://danny4897.github.io/MonadicSharp-OpenCode/' },
              { text: 'AgentScope', link: 'https://danny4897.github.io/AgentScope/' },
            ],
          },
        ],
      },
    ],

    sidebar: {
      '/': [
        {
          text: 'Guide',
          items: [
            { text: 'Getting Started', link: '/getting-started' },
            { text: 'Architecture', link: '/architecture' },
            { text: 'Deploy', link: '/deploy' },
          ],
        },
        {
          text: 'Features',
          items: [
            { text: 'Pipeline Tracing', link: '/features/tracing' },
            { text: 'Metrics Dashboard', link: '/features/metrics' },
            { text: 'Circuit Breakers', link: '/features/circuit-breaker' },
            { text: 'Alerts', link: '/features/alerts' },
          ],
        },
        {
          text: 'Integrations',
          items: [
            { text: 'MonadicSharp.Telemetry', link: '/integrations/telemetry' },
            { text: 'Semantic Kernel', link: '/integrations/semantic-kernel' },
            { text: 'Microsoft.Extensions.AI', link: '/integrations/extensions-ai' },
          ],
        },
        {
          text: 'Ecosystem',
          collapsed: true,
          items: [
            { text: 'MonadicSharp ↗', link: 'https://danny4897.github.io/MonadicSharp/' },
            { text: 'MonadicSharp.Framework ↗', link: 'https://danny4897.github.io/MonadicSharp.Framework/' },
            { text: 'MonadicSharp.AI ↗', link: 'https://danny4897.github.io/MonadicSharp.AI/' },
            { text: 'MonadicSharp.Recovery ↗', link: 'https://danny4897.github.io/MonadicSharp.Recovery/' },
            { text: 'MonadicSharp.Azure ↗', link: 'https://danny4897.github.io/MonadicSharp.Azure/' },
            { text: 'MonadicSharp.DI ↗', link: 'https://danny4897.github.io/MonadicSharp.DI/' },
            { text: 'MonadicLeaf ↗', link: 'https://danny4897.github.io/MonadicLeaf/' },
            { text: 'MonadicSharp × OpenCode ↗', link: 'https://danny4897.github.io/MonadicSharp-OpenCode/' },
            { text: 'AgentScope ↗', link: 'https://danny4897.github.io/AgentScope/' },
          ],
        },
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Danny4897/AgentScope' },
    ],

    search: { provider: 'local' },

    editLink: {
      pattern: 'https://github.com/Danny4897/AgentScope/edit/feat-001-First-implementations/docs/:path',
      text: 'Edit this page on GitHub',
    },


    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2024–2026 Danny4897',
    },

    outline: { level: [2, 3], label: 'On this page' },
  },

  markdown: {
    theme: { light: 'github-light', dark: 'one-dark-pro' },
  },
})
