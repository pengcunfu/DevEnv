<script setup lang="ts">
const version = __APP_VERSION__

const environments = [
  { name: 'PHP', version: '8.3.12', tone: 'violet', status: '运行中' },
  { name: 'Node.js', version: '20.11.1', tone: 'green', status: '已停止' },
  { name: 'Python', version: '3.12.2', tone: 'blue', status: '运行中' },
  { name: 'MySQL', version: '8.0.36', tone: 'amber', status: '已停止' },
]

const bars = [24, 38, 30, 46, 40, 58, 44, 68, 54, 76, 62, 84, 70, 92, 76, 88, 66, 74, 58, 64]
</script>

<template>
  <div class="preview" aria-hidden="true">
    <div class="preview-bar">
      <span class="dot red"></span>
      <span class="dot amber"></span>
      <span class="dot green"></span>
      <span class="preview-title">燧岚开发盒子 — DevEnv</span>
      <span class="preview-version">v{{ version }}</span>
    </div>

    <div class="preview-body">
      <aside class="preview-side">
        <div class="preview-brand">
          <img src="/logo.svg" alt="" width="20" height="20" />
          <span>燧岚开发盒子</span>
        </div>
        <ul>
          <li>应用与服务</li>
          <li>开发环境</li>
          <li class="active"><span class="preview-flame"></span>熔岩环境</li>
          <li>熔岩翻译</li>
          <li>网络监控</li>
          <li>进程监控</li>
          <li>熔岩剪贴板</li>
          <li>系统工具</li>
        </ul>
        <div class="preview-side-foot">设置</div>
      </aside>

      <div class="preview-main">
        <div class="preview-main-head">
          <div>
            <strong>熔岩环境</strong>
            <span>绿色版进程 · 下载 · 镜像</span>
          </div>
          <span class="preview-pill">已扫描 4 个环境</span>
        </div>

        <div class="preview-cards">
          <div v-for="env in environments" :key="env.name" class="preview-card">
            <div class="preview-card-top">
              <span class="env-logo" :class="env.tone">{{ env.name.slice(0, 2) }}</span>
              <span class="env-meta">
                <strong>{{ env.name }}</strong>
                <small>{{ env.version }}</small>
              </span>
            </div>
            <div class="preview-card-foot">
              <span class="status-dot" :class="{ on: env.status === '运行中' }"></span>
              {{ env.status }}
            </div>
          </div>
        </div>

        <div class="preview-panel">
          <div class="preview-panel-head">
            <span>实时网络</span>
            <span class="preview-rate">↓ 3.8 MB/s · ↑ 862 KB/s</span>
          </div>
          <div class="preview-bars">
            <i v-for="(height, index) in bars" :key="index" :style="{ height: `${height}%` }"></i>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.preview {
  position: relative;
  overflow: hidden;
  border: 1px solid rgba(45, 212, 191, 0.22);
  border-radius: 16px;
  background: #0b1220;
  box-shadow:
    0 30px 70px rgba(2, 6, 23, 0.45),
    0 0 0 1px rgba(255, 255, 255, 0.03) inset;
  color: #e2e8f0;
  font-size: 12px;
}

.preview-bar {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 36px;
  padding: 0 12px;
  border-bottom: 1px solid rgba(148, 163, 184, 0.14);
  background: rgba(15, 23, 42, 0.9);
}

.dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
}

.dot.red {
  background: #f87171;
}

.dot.amber {
  background: #fbbf24;
}

.dot.green {
  background: #34d399;
}

.preview-title {
  margin-left: 8px;
  color: #94a3b8;
  font-size: 11px;
}

.preview-version {
  margin-left: auto;
  padding: 1px 7px;
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 999px;
  color: #64748b;
  font-size: 10px;
}

.preview-body {
  display: grid;
  grid-template-columns: 150px 1fr;
  min-height: 330px;
}

.preview-side {
  display: flex;
  flex-direction: column;
  padding: 14px 10px;
  border-right: 1px solid rgba(148, 163, 184, 0.12);
  background: rgba(8, 15, 30, 0.75);
}

.preview-brand {
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 0 6px 14px;
  color: #f1f5f9;
  font-size: 11.5px;
  font-weight: 700;
}

.preview-brand img {
  border-radius: 5px;
}

.preview-side ul {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.preview-side li {
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 6px 8px;
  border-radius: 6px;
  color: #94a3b8;
  font-size: 11px;
}

.preview-side li.active {
  background: rgba(45, 212, 191, 0.12);
  color: #5eead4;
  font-weight: 600;
}

.preview-flame {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #f59e0b;
  box-shadow: 0 0 8px rgba(245, 158, 11, 0.8);
}

.preview-side-foot {
  margin-top: auto;
  padding: 8px;
  color: #475569;
  font-size: 11px;
}

.preview-main {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 16px;
}

.preview-main-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
}

.preview-main-head strong {
  display: block;
  color: #f1f5f9;
  font-size: 15px;
}

.preview-main-head span {
  color: #64748b;
  font-size: 10.5px;
}

.preview-pill {
  padding: 3px 9px;
  border: 1px solid rgba(45, 212, 191, 0.3);
  border-radius: 999px;
  background: rgba(45, 212, 191, 0.1);
  color: #5eead4 !important;
  white-space: nowrap;
}

.preview-cards {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 9px;
}

.preview-card {
  padding: 10px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 9px;
  background: rgba(15, 23, 42, 0.7);
}

.preview-card-top {
  display: flex;
  align-items: center;
  gap: 8px;
}

.env-logo {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 7px;
  font-size: 9px;
  font-weight: 800;
  letter-spacing: -0.02em;
}

.env-logo.violet {
  background: rgba(167, 139, 250, 0.16);
  color: #c4b5fd;
}

.env-logo.green {
  background: rgba(52, 211, 153, 0.16);
  color: #6ee7b7;
}

.env-logo.blue {
  background: rgba(96, 165, 250, 0.16);
  color: #93c5fd;
}

.env-logo.amber {
  background: rgba(251, 191, 36, 0.16);
  color: #fcd34d;
}

.env-meta strong {
  display: block;
  color: #e2e8f0;
  font-size: 11px;
  font-weight: 650;
}

.env-meta small {
  color: #64748b;
  font-size: 9.5px;
}

.preview-card-foot {
  display: flex;
  align-items: center;
  gap: 5px;
  margin-top: 9px;
  color: #64748b;
  font-size: 9.5px;
}

.status-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: #475569;
}

.status-dot.on {
  background: #34d399;
  box-shadow: 0 0 6px rgba(52, 211, 153, 0.8);
}

.preview-panel {
  flex: 1;
  padding: 11px 12px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 9px;
  background: rgba(15, 23, 42, 0.55);
}

.preview-panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  color: #94a3b8;
  font-size: 10px;
}

.preview-rate {
  color: #5eead4;
  font-family: var(--font-mono);
  font-size: 9.5px;
}

.preview-bars {
  display: flex;
  align-items: flex-end;
  gap: 3px;
  height: 62px;
  margin-top: 10px;
}

.preview-bars i {
  flex: 1;
  border-radius: 2px 2px 0 0;
  background: linear-gradient(180deg, rgba(45, 212, 191, 0.9), rgba(13, 148, 136, 0.25));
}

@media (max-width: 560px) {
  .preview-body {
    grid-template-columns: 1fr;
  }

  .preview-side {
    display: none;
  }
}
</style>
