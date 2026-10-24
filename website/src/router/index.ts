import type { RouteRecordRaw } from 'vue-router'
import { docs } from '@/docs/registry'
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
