import { HashRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from '@/components/Layout'
import { AppsPage } from '@/pages/AppsPage'
import { ClipboardPage } from '@/pages/ClipboardPage'
import { DevEnvPage } from '@/pages/DevEnvPage'
import { FoldersPage } from '@/pages/FoldersPage'
import { LavaPage } from '@/pages/LavaPage'
import { NetworkPage } from '@/pages/NetworkPage'
import { ProcessesPage } from '@/pages/ProcessesPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { ToolsPage } from '@/pages/ToolsPage'
import { TranslatorPage } from '@/pages/TranslatorPage'
import { WebsPage } from '@/pages/WebsPage'
import { JsonToolPage } from '@/pages/utils/JsonToolPage'
import { HostsToolPage } from '@/pages/utils/HostsToolPage'
import { HashToolPage } from '@/pages/utils/HashToolPage'
import { ImageToolPage } from '@/pages/utils/ImageToolPage'
import { StartupToolPage } from '@/pages/utils/StartupToolPage'

export default function App() {
  return (
    <HashRouter>
      <Routes>
        {/* 独立剪贴板窗口：无主壳侧栏 / 菜单 */}
        <Route path="clipboard-window" element={<ClipboardPage standalone />} />
        <Route element={<Layout />}>
          <Route index element={<Navigate to="/apps" replace />} />
          <Route path="apps" element={<AppsPage />} />
          <Route path="folders" element={<FoldersPage />} />
          <Route path="webs" element={<WebsPage />} />
          <Route path="devenv" element={<DevEnvPage />} />
          <Route path="lava" element={<LavaPage />} />
          <Route path="translator" element={<TranslatorPage />} />
          <Route path="network" element={<NetworkPage />} />
          <Route path="processes" element={<ProcessesPage />} />
          <Route path="clipboard" element={<ClipboardPage />} />
          <Route path="tools" element={<ToolsPage />} />
          <Route path="tools/json" element={<JsonToolPage />} />
          <Route path="tools/hosts" element={<HostsToolPage />} />
          <Route path="tools/hash" element={<HashToolPage />} />
          <Route path="tools/image" element={<ImageToolPage />} />
          <Route path="tools/startup" element={<StartupToolPage />} />
          <Route path="settings" element={<SettingsPage />} />
        </Route>
      </Routes>
    </HashRouter>
  )
}
