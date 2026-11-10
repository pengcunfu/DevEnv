<script setup lang="ts">
import { RouterLink } from 'vue-router'
import AppPreview from '@/components/AppPreview.vue'
import FeatureIcon from '@/components/FeatureIcon.vue'
import { repoUrl, releasesUrl } from '@/docs/nav'
import { softwareSchema, sourceCodeSchema, websiteSchema } from '@/seo/schema'
import { useSeo } from '@/seo/useSeo'

const version = __APP_VERSION__

const description =
  'FireNeb DevEnv：Windows 开发环境管理工具，集成熔岩绿色版环境、网页翻译、网络监控、进程监控、服务监控与剪贴板历史。基于 .NET 10 + WPF，Apache 2.0 开源。'

useSeo({
  title: 'FireNeb DevEnv — Windows 开发环境管理工具',
  description,
  path: '/',
  jsonLd: [
    websiteSchema(),
    softwareSchema(version, description),
    sourceCodeSchema(version, description),
  ],
})

const features = [
  {
    icon: 'apps',
    title: '应用与服务',
    description: '收藏本地 / Web 工具与文件夹快捷方式，管理本机常见开发服务启停。',
    link: '/guide/features',
    external: false,
  },
  {
    icon: 'lava',
    title: '熔岩环境',
    description: '绿色版进程管理、软件下载、环境扫描与镜像配置，全部收在一个页面里。',
    link: '/guide/lava/getting-started',
    external: false,
  },
  {
    icon: 'translate',
    title: '熔岩翻译',
    description: 'WebView 内嵌多站点网页翻译，Alt+Space 一键填入剪贴板文本。',
    link: '/guide/translator/getting-started',
    external: false,
  },
  {
    icon: 'network',
    title: '网络监控',
    description: '按进程查看连接与上下行速率，内置测速与端口管理。',
    link: '/guide/networkwatch/getting-started',
    external: false,
  },
  {
    icon: 'process',
    title: '进程监控',
    description: '实时查看进程 CPU / 内存，支持搜索排序与结束进程。',
    link: '/guide/processwatch/getting-started',
    external: false,
  },
  {
    icon: 'service',
    title: '服务监控',
    description: '内置 Windows 服务面板，查看状态、依赖并启停与修改启动类型。',
    link: '/guide/servicewatch/getting-started',
    external: false,
  },
  {
    icon: 'clipboard',
    title: '熔岩剪贴板',
    description: '文本与图片剪贴板历史，全局热键一键唤起。',
    link: '/guide/clipboard/getting-started',
    external: false,
  },
  {
    icon: 'tools',
    title: '系统工具',
    description: 'Windows 管理工具快捷入口，以及 JSON / Hosts / 图像转换 / 哈希等实用工具。',
    link: '/guide/features',
    external: false,
  },
  {
    icon: 'code',
    title: '开源免费',
    description: '基于 .NET 10 + WPF，Apache 2.0 协议，源码完全公开，本地数据归你所有。',
    link: repoUrl,
    external: true,
  },
]

const steps = [
  {
    index: '01',
    title: '下载并安装',
    description: '从 GitHub Releases 获取 Windows x64 安装器；缺少 .NET 10 Desktop Runtime 时会自动安装。',
    link: '/guide/getting-started',
    linkText: '安装说明',
  },
  {
    index: '02',
    title: '整理开发环境',
    description: '把常用工具、网站与文件夹放进「应用与服务」，用「熔岩环境」管理绿色版软件与镜像。',
    link: '/guide/lava/getting-started',
    linkText: '熔岩环境',
  },
  {
    index: '03',
    title: '随时唤起',
    description: '剪贴板历史、网页翻译与系统工具都有全局热键，不必在窗口之间来回切换。',
    link: '/guide/features',
    linkText: '功能概览',
  },
]
</script>

<template>
  <div class="home">
    <section class="hero">
      <div class="hero-grid" aria-hidden="true"></div>
      <div class="hero-glow" aria-hidden="true"></div>

      <div class="container hero-inner">
        <div class="hero-copy">
          <p class="eyebrow">
            <span class="pulse"></span>
            FireNeb · v{{ version }} · Apache 2.0 开源 · Windows 10+
          </p>
          <h1>
            <span class="hero-name">FireNeb DevEnv</span>
          </h1>
          <p class="hero-tagline">
            Windows 开发环境管理工具。把日常开发要用的环境、翻译、监控与剪贴板，收进同一个盒子里。
          </p>

          <div class="hero-actions">
            <a class="btn btn-primary" :href="releasesUrl" target="_blank" rel="noopener noreferrer">
              <FeatureIcon name="download" :size="17" />
              下载 v{{ version }}
            </a>
            <RouterLink class="btn btn-secondary" to="/guide/getting-started">
              快速开始
            </RouterLink>
            <a class="btn btn-ghost" :href="repoUrl" target="_blank" rel="noopener noreferrer">
              GitHub
            </a>
          </div>

          <ul class="hero-meta">
            <li>Windows 10 / 11 · x64</li>
            <li>.NET 10 · WPF</li>
            <li>数据保存在本地</li>
          </ul>
        </div>

        <div class="hero-visual">
          <AppPreview />
        </div>
      </div>
    </section>

    <section class="section" id="features">
      <div class="container">
        <header class="section-head">
          <p class="eyebrow">功能</p>
          <h2>把开发环境收进一个盒子</h2>
          <p>不再为每个小工具单独装一个程序，常用能力都在同一个壳里，数据也只留在本机。</p>
        </header>

        <div class="feature-grid">
          <component
            :is="feature.external ? 'a' : RouterLink"
            v-for="feature in features"
            :key="feature.title"
            v-bind="
              feature.external
                ? { href: feature.link, target: '_blank', rel: 'noopener noreferrer' }
                : { to: feature.link }
            "
            class="feature-card"
          >
            <span class="feature-icon">
              <FeatureIcon :name="feature.icon" :size="21" />
            </span>
            <h3>{{ feature.title }}</h3>
            <p>{{ feature.description }}</p>
            <span class="feature-link">了解更多 →</span>
          </component>
        </div>
      </div>
    </section>

    <section class="section section-alt" id="workflow">
      <div class="container">
        <header class="section-head">
          <p class="eyebrow">上手</p>
          <h2>三步开始使用</h2>
          <p>从安装到日常使用，通常只需要几分钟。</p>
        </header>

        <div class="step-grid">
          <div v-for="step in steps" :key="step.index" class="step-card">
            <span class="step-index">{{ step.index }}</span>
            <h3>{{ step.title }}</h3>
            <p>{{ step.description }}</p>
            <RouterLink class="step-link" :to="step.link">{{ step.linkText }} →</RouterLink>
          </div>
        </div>
      </div>
    </section>

    <section class="section" id="download">
      <div class="container download-panel">
        <div class="download-copy">
          <p class="eyebrow">下载</p>
          <h2>下载 FireNeb DevEnv v{{ version }}</h2>
          <p>
            提供 Windows x64 安装包与便携版。安装器为框架依赖发布，缺少 .NET 10 Desktop Runtime
            时会自动安装。
          </p>
          <ul class="check-list">
            <li>Windows 10 或更高版本（64 位）</li>
            <li>升级 / 重装不会丢失用户数据</li>
            <li>应用内可检查并自动更新</li>
          </ul>
          <div class="hero-actions">
            <a class="btn btn-primary" :href="releasesUrl" target="_blank" rel="noopener noreferrer">
              前往 GitHub Releases
            </a>
            <RouterLink class="btn btn-secondary" to="/guide/releases">版本说明</RouterLink>
            <RouterLink class="btn btn-ghost" to="/downloads">软件下载站 →</RouterLink>
          </div>
        </div>

        <div class="download-card">
          <div class="download-card-head">
            <img src="/logo.svg" alt="" width="44" height="44" />
            <div>
              <strong>DevEnv-win-Setup.exe</strong>
              <span>Windows x64 · v{{ version }}</span>
            </div>
          </div>
          <div class="download-card-rows">
            <div><span>运行时</span><strong>.NET 10 Desktop Runtime</strong></div>
            <div><span>许可</span><strong>Apache License 2.0</strong></div>
            <div><span>数据目录</span><strong>文档\FNSoftware\DevEnv</strong></div>
          </div>
          <p class="download-note">安装包未签名，SmartScreen 提示时选择「仍要运行」即可。</p>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.home {
  overflow: hidden;
}

.container {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 24px;
}

.eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin: 0 0 16px;
  color: var(--brand);
  font-size: 12.5px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.pulse {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--brand);
  box-shadow: 0 0 0 4px var(--brand-soft);
}

/* Hero */
.hero {
  position: relative;
  padding: 76px 0 84px;
  background: var(--hero-bg);
}

.hero-grid {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(var(--hero-grid-line) 1px, transparent 1px),
    linear-gradient(90deg, var(--hero-grid-line) 1px, transparent 1px);
  background-size: 56px 56px;
  mask-image: radial-gradient(ellipse 75% 65% at 50% 20%, #000 30%, transparent 75%);
  opacity: 0.55;
}

.hero-glow {
  position: absolute;
  top: -180px;
  left: 50%;
  width: 780px;
  height: 560px;
  border-radius: 50%;
  background: radial-gradient(circle, var(--hero-glow) 0%, transparent 65%);
  transform: translateX(-50%);
  pointer-events: none;
}

.hero-inner {
  position: relative;
  display: grid;
  grid-template-columns: 1.02fr 1fr;
  gap: 56px;
  align-items: center;
}

.hero-copy h1 {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin: 0 0 22px;
  font-size: clamp(44px, 6vw, 68px);
  line-height: 1.05;
  letter-spacing: -0.035em;
}

.hero-name {
  background: linear-gradient(100deg, var(--brand) 10%, #38bdf8 55%, var(--accent) 100%);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.hero-tagline {
  max-width: 520px;
  margin: 0 0 30px;
  color: var(--text-soft);
  font-size: 17px;
  line-height: 1.75;
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  height: 44px;
  padding: 0 20px;
  border: 1px solid transparent;
  border-radius: 11px;
  font-size: 14.5px;
  font-weight: 650;
  text-decoration: none;
  cursor: pointer;
  transition: transform 0.15s ease, box-shadow 0.15s ease, background 0.15s ease;
}

.btn:hover {
  transform: translateY(-1px);
}

.btn-primary {
  background: var(--brand);
  color: var(--on-brand);
  box-shadow: 0 10px 26px color-mix(in srgb, var(--brand) 32%, transparent);
}

.btn-primary:hover {
  background: var(--brand-strong);
}

.btn-secondary {
  border-color: var(--border-strong);
  background: var(--surface);
  color: var(--text);
}

.btn-secondary:hover {
  border-color: var(--brand);
  color: var(--brand);
}

.btn-ghost {
  color: var(--text-soft);
}

.btn-ghost:hover {
  background: var(--surface-2);
  color: var(--text);
}

.hero-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 22px;
  margin: 28px 0 0;
  padding: 0;
  list-style: none;
  color: var(--muted);
  font-size: 12.5px;
}

.hero-meta li::before {
  content: '·';
  margin-right: 8px;
  color: var(--brand);
  font-weight: 700;
}

.hero-visual {
  position: relative;
}

/* Sections */
.section {
  padding: 88px 0;
}

.section-alt {
  background: var(--surface-2);
}

.section-head {
  max-width: 640px;
  margin: 0 auto 44px;
  text-align: center;
}

.section-head h2 {
  margin: 0 0 14px;
  color: var(--text);
  font-size: clamp(26px, 3.4vw, 36px);
  letter-spacing: -0.025em;
}

.section-head p {
  margin: 0;
  color: var(--muted);
  font-size: 15px;
  line-height: 1.75;
}

/* Features */
.feature-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.feature-card {
  display: flex;
  flex-direction: column;
  padding: 22px 20px;
  border: 1px solid var(--border);
  border-radius: 14px;
  background: var(--surface);
  color: inherit;
  text-decoration: none;
  transition: border-color 0.18s ease, transform 0.18s ease, box-shadow 0.18s ease;
}

.feature-card:hover {
  border-color: color-mix(in srgb, var(--brand) 45%, var(--border));
  transform: translateY(-3px);
  box-shadow: 0 16px 40px rgba(2, 6, 23, 0.08);
}

.feature-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  margin-bottom: 16px;
  border-radius: 11px;
  background: var(--brand-soft);
  color: var(--brand);
}

.feature-card h3 {
  margin: 0 0 8px;
  color: var(--text);
  font-size: 15.5px;
}

.feature-card p {
  margin: 0 0 16px;
  color: var(--muted);
  font-size: 13px;
  line-height: 1.7;
}

.feature-link {
  margin-top: auto;
  color: var(--brand);
  font-size: 12.5px;
  font-weight: 600;
  opacity: 0;
  transform: translateX(-4px);
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.feature-card:hover .feature-link {
  opacity: 1;
  transform: translateX(0);
}

/* Steps */
.step-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 18px;
}

.step-card {
  position: relative;
  padding: 26px 24px;
  border: 1px solid var(--border);
  border-radius: 14px;
  background: var(--surface);
}

.step-index {
  display: inline-block;
  margin-bottom: 18px;
  color: color-mix(in srgb, var(--brand) 55%, var(--muted));
  font-family: var(--font-mono);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.12em;
}

.step-card h3 {
  margin: 0 0 10px;
  color: var(--text);
  font-size: 17px;
}

.step-card p {
  margin: 0 0 18px;
  color: var(--muted);
  font-size: 13.5px;
  line-height: 1.75;
}

.step-link {
  color: var(--brand);
  font-size: 13px;
  font-weight: 600;
  text-decoration: none;
}

.step-link:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}

/* Download */
.download-panel {
  display: grid;
  grid-template-columns: 1.15fr 0.85fr;
  gap: 48px;
  align-items: center;
  padding: 48px;
  border: 1px solid var(--border);
  border-radius: 22px;
  background:
    radial-gradient(ellipse 60% 80% at 100% 0%, var(--brand-soft), transparent 60%),
    var(--surface);
}

.download-copy h2 {
  margin: 0 0 16px;
  color: var(--text);
  font-size: clamp(26px, 3vw, 34px);
  letter-spacing: -0.02em;
}

.download-copy > p {
  margin: 0 0 18px;
  color: var(--muted);
  font-size: 14.5px;
  line-height: 1.75;
}

.check-list {
  display: flex;
  flex-direction: column;
  gap: 9px;
  margin: 0 0 26px;
  padding: 0;
  list-style: none;
}

.check-list li {
  position: relative;
  padding-left: 24px;
  color: var(--text-soft);
  font-size: 13.5px;
}

.check-list li::before {
  content: '✓';
  position: absolute;
  left: 0;
  top: -1px;
  color: var(--brand);
  font-weight: 800;
}

.download-card {
  padding: 22px;
  border: 1px solid var(--border);
  border-radius: 16px;
  background: var(--surface);
  box-shadow: 0 18px 45px rgba(2, 6, 23, 0.08);
}

.download-card-head {
  display: flex;
  align-items: center;
  gap: 13px;
  padding-bottom: 18px;
  border-bottom: 1px solid var(--border);
}

.download-card-head img {
  border-radius: 10px;
}

.download-card-head strong {
  display: block;
  color: var(--text);
  font-size: 14.5px;
}

.download-card-head span {
  color: var(--muted);
  font-size: 12px;
}

.download-card-rows {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 18px 0;
}

.download-card-rows div {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 16px;
}

.download-card-rows span {
  color: var(--muted);
  font-size: 12px;
  white-space: nowrap;
}

.download-card-rows strong {
  color: var(--text-soft);
  font-family: var(--font-mono);
  font-size: 11.5px;
  font-weight: 600;
  text-align: right;
}

.download-note {
  margin: 0;
  padding-top: 14px;
  border-top: 1px solid var(--border);
  color: var(--muted);
  font-size: 11.5px;
  line-height: 1.6;
}

@media (max-width: 1020px) {
  .hero-inner {
    grid-template-columns: 1fr;
    gap: 44px;
  }

  .hero-copy {
    max-width: 640px;
  }

  .hero-visual {
    max-width: 660px;
  }

  .feature-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .download-panel {
    grid-template-columns: 1fr;
    padding: 36px 30px;
  }
}

@media (max-width: 720px) {
  .hero {
    padding: 52px 0 60px;
  }

  .section {
    padding: 62px 0;
  }

  .step-grid {
    grid-template-columns: 1fr;
  }

  .feature-grid {
    grid-template-columns: 1fr;
  }

  .hero-actions .btn {
    flex: 1 1 auto;
  }
}
</style>
