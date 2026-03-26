import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'AgentScope',
  description: 'AI agent observability platform for .NET — see every agent, trace every pipeline, catch every failure.',
  base: '/AgentScope/',
  cleanUrls: true,

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
          { text: 'MonadicSharp Core', link: 'https://danny4897.github.io/MonadicSharp/' },
          { text: 'GitHub', link: 'https://github.com/Danny4897/AgentScope' },
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
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Danny4897/AgentScope' },
    ],

    search: { provider: 'local' },

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
