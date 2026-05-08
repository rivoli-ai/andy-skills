import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface NamespaceDto {
  id: string;
  slug: string;
  displayName: string;
  description?: string | null;
  visibility: string;
  createdAtUtc: string;
}

export interface PackageSummaryDto {
  id: string;
  namespaceSlug: string;
  slug: string;
  title: string;
  description?: string | null;
  createdAtUtc: string;
  latestVersion?: string | null;
  hasLatest: boolean;
}

export interface SkillVersionDto {
  id: string;
  packageId: string;
  version: string;
  tag?: string | null;
  isLatest: boolean;
  artifactUri: string;
  publishedAtUtc: string;
  /** True when the registry holds the ZIP in Postgres (preview / install URL). */
  hasStoredZip: boolean;
}

export interface CreateNamespaceBody {
  slug: string;
  displayName: string;
  description?: string | null;
  visibility?: string | null;
}

export function registryApiErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as { error?: string } | undefined;
    if (body?.error && typeof body.error === 'string') {
      return body.error;
    }
    if (err.status === 0) {
      return 'Network error — is the API running on port 5289?';
    }
    return err.message || `HTTP ${err.status}`;
  }
  return 'Unexpected error';
}

@Injectable({ providedIn: 'root' })
export class RegistryApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  listNamespaces(): Observable<NamespaceDto[]> {
    return this.http.get<NamespaceDto[]>(`${this.base}/api/namespaces`);
  }

  createNamespace(body: CreateNamespaceBody): Observable<NamespaceDto> {
    return this.http.post<NamespaceDto>(`${this.base}/api/namespaces`, body);
  }

  listPackages(namespaceSlug: string): Observable<PackageSummaryDto[]> {
    return this.http.get<PackageSummaryDto[]>(
      `${this.base}/api/namespaces/${encodeURIComponent(namespaceSlug)}/packages`,
    );
  }

  createPackage(
    namespaceSlug: string,
    body: { slug: string; title: string; description?: string | null },
  ): Observable<PackageSummaryDto> {
    const ns = encodeURIComponent(namespaceSlug);
    return this.http.post<PackageSummaryDto>(`${this.base}/api/namespaces/${ns}/packages`, body);
  }

  listVersions(namespaceSlug: string, skillSlug: string): Observable<SkillVersionDto[]> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.get<SkillVersionDto[]>(
      `${this.base}/api/namespaces/${ns}/packages/${sk}/versions`,
    );
  }

  publishVersion(
    namespaceSlug: string,
    skillSlug: string,
    body: { version: string; tag?: string | null; artifactUri: string },
  ): Observable<SkillVersionDto> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.post<SkillVersionDto>(
      `${this.base}/api/namespaces/${ns}/packages/${sk}/versions`,
      body,
    );
  }

  getSkillMarkdown(namespaceSlug: string, skillSlug: string, version: string): Observable<string> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    const v = encodeURIComponent(version);
    return this.http.get(`${this.base}/api/namespaces/${ns}/packages/${sk}/versions/${v}/SKILL.md`, {
      responseType: 'text',
    });
  }

  /** Multipart upload; backend stores ZIP in Postgres and sets artifactUri to the install URL. */
  publishVersionFromZip(
    namespaceSlug: string,
    skillSlug: string,
    body: { version: string; tag?: string | null; file: File },
  ): Observable<SkillVersionDto> {
    const fd = new FormData();
    fd.append('version', body.version);
    const tag = body.tag?.trim();
    if (tag) {
      fd.append('tag', tag);
    }
    fd.append('file', body.file, body.file.name);
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.post<SkillVersionDto>(
      `${this.base}/api/namespaces/${ns}/packages/${sk}/versions/upload`,
      fd,
    );
  }
}
