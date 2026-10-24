import type { RouteRecordRaw } from 'vue-router'
import { docs } from '@/docs/registry'
import {
  categoryAnchor,
  categoryPagePath,
  downloadCategories,
} from '@/downloads/software'
import DownloadsView from '@/views/DownloadsView.vue'
import HomeView from '@/views/HomeView.vue'
import NotFoundView from '@/views/NotFoundView.vue'

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'home',
    component: HomeView,
  },
  {
    path: '/downloads',
    name: 'downloads',
    component: DownloadsView,
  },
  // 每个分类生成独立静态页（/downloads/java），便于搜索引擎收录长尾关键词。
  ...downloadCategories.map(
    (category): RouteRecordRaw => ({
      path: categoryPagePath(category),
      name: `downloads-${categoryAnchor(category)}`,
      component: DownloadsView,
      meta: { category: categoryAnchor(category) },
    }),
  ),
  ...docs.map(
    (doc): RouteRecordRaw => ({
      path: doc.path,
      name: doc.path,
      component: () => import('@/views/DocView.vue'),
      meta: { docPath: doc.path },
    }),
  ),
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: NotFoundView,
  },
]
